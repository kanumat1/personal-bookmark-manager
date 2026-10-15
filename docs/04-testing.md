## Test Strategy

Testing was planned as a combination of automated unit tests and manual browser verification because the application has both service/repository behavior and a local web UI.

### Automated testing

- Framework: .NET test project using the existing unit-test suite.
- Service/repository behavior covered includes bookmark creation, validation, duplicate handling, tag normalization/filtering, search, edit, delete, and JSON persistence behavior.
- Final local verification was completed against the submitted application state: `dotnet build` passed, and `dotnet test` passed with **51 tests passed, 0 failed, 0 skipped**.

### Manual testing

The browser UI was manually exercised for the mandatory user flows: add bookmark, tags, newest-first listing, tag filtering, search, edit, delete, validation, duplicate handling, empty/error states, and persistence after restart.

### Non-functional checks

- **Performance:** the application uses in-memory bookmark data and in-memory filtering/search for the expected local exercise scale. A formal 1,000-bookmark timing benchmark has not yet been recorded.
- **Reliability/persistence:** JSON persistence and restart behavior were rechecked during the final manual regression and passed.
- **Usability/accessibility:** labels, status/error feedback, and keyboard/browser behavior were reviewed during implementation. The UI uses accessible status/error regions.
- **Security:** untrusted URL input was checked for invalid schemes such as `javascript:`. The title-fetch implementation also restricts external requests and re-checks redirect destinations to reduce SSRF risk. Frontend-rendered bookmark data uses text content rather than HTML injection.

## Test Matrix

| Requirement | Scenario | Expected result | Actual result | Pass/Fail | AI helped create the test |
|---|---|---|---|---|---|
| Add Bookmark | Add a valid URL with a supplied title | Bookmark is saved and displayed | Valid bookmark was saved during implementation/browser verification | Pass | Yes |
| Add Bookmark | Omit title for a page with a usable HTML title | Page title is fetched and used | Title-fetch behavior was implemented and smoke-tested | Pass | Yes |
| Add Bookmark | Omit title when title retrieval fails | User is asked to provide a title; bookmark is not saved without one | This behavior was manually exercised for a page without a usable title | Pass | Yes |
| Tags | Add multiple tags, including different casing/spacing | Tags are normalized, deduplicated, and usable for filtering | Tags worked after normalization/filter fix | Pass | Yes |
| List/Newest First | Add multiple bookmarks | Most recently created bookmark appears first | Verified during User Story 4 browser testing | Pass | Yes |
| Filter by Tag | Select an existing tag | Only matching bookmarks are displayed | Initially failed for mixed-case `Nagarro AI`; fixed and retested successfully | Pass | Yes |
| Filter by Tag | Select `Untagged` | Only bookmarks with zero tags are displayed | Implemented as a virtual filter and exercised during UI verification | Pass | Yes |
| Search | Search by title | Matching bookmarks are shown | Search behavior was implemented and browser-smoke-tested | Pass | Yes |
| Search | Search by URL | Matching bookmarks are shown | Search behavior was implemented and browser-smoke-tested | Pass | Yes |
| Edit | Change URL, title, or tags | Existing bookmark is updated without creating a second record | Edit behavior was implemented and smoke-tested | Pass | Yes |
| Delete | Delete an existing bookmark | Bookmark is removed after confirmation | Delete behavior was implemented and smoke-tested, including API verification | Pass | Yes |
| Persistence | Stop/restart application | Previously saved bookmarks remain available | Final manual regression confirmed bookmarks remained available after stopping and restarting the application | Pass | Yes |
| Validation | Empty/invalid URL | Bookmark is not saved and understandable feedback is shown | Invalid and empty URL validation was verified | Pass | Yes |
| Duplicate Handling | Add the same URL more than once | Duplicate is detected and rejected | Duplicate rejection was smoke-tested | Pass | Yes |
| Empty/Error States | No bookmarks / no search matches / title-fetch failure | User sees an understandable empty/error message | Empty/no-result/title-fetch states were handled in the UI | Pass | Yes |


## AI-Discovered Edge Cases

The following cases were raised during AI-assisted planning, design, or implementation and were useful beyond the basic happy path:

1. **Title retrieval fails or returns no usable title.** The final behavior does not save a placeholder title; the user is asked to provide a title.
2. **Unsafe URL schemes.** URLs such as `javascript:` must be rejected rather than treated as normal web URLs.
3. **Redirects during title fetching.** Redirect destinations need to be checked as well as the original URL to avoid bypassing the URL safety restriction.
4. **Case differences in tags.** Tag matching should be case-insensitive and normalization should prevent duplicate logical tags.
5. **Untagged bookmarks.** A bookmark with no tags needs a usable filter state even though `Untagged` is not stored as a real tag.
6. **Duplicate URL during edit.** Editing a bookmark must exclude the bookmark being edited from the duplicate check, while still rejecting a URL used by another bookmark.
7. **Search with no matches.** The UI should distinguish a legitimate empty result from an application failure.

These cases were covered by automated tests, manual verification, or both. The final regression confirmed the main user flows on the submitted application state.

## Fail -> Fix -> Retest

### Failure 1 — Case-sensitive tag filtering

**Failure:** Filtering for the tag `Nagarro AI` returned no bookmark even though a bookmark containing that tag existed.

**Diagnosis:** The stored tag had mixed casing while the filter comparison had normalized the requested tag differently. The comparison was effectively case-sensitive.

**Fix:** The tag comparison was changed to use case-insensitive comparison (`StringComparer.OrdinalIgnoreCase`).

**Retest:** The same `Nagarro AI` filter was exercised again and the bookmark was returned correctly. The user confirmed that the filter was working after the change.

### Failure 2 — Stale browser/application state during filter testing

**Failure:** The browser initially showed missing tags/bookmarks even though the API and application files contained the expected data.

**Diagnosis:** The running application/API responses were checked and the served frontend JavaScript was inspected. The behavior was traced to stale browser/application state rather than missing backend functionality.

**Fix:** The stale state was cleared by reloading the application appropriately; a fresh load/Incognito check was used to distinguish caching from an API problem. A stale application process/file lock was also encountered during one development restart and was resolved by stopping the old process before rebuilding.

**Retest:** The fresh application load returned the expected bookmark/tag data and allowed the filter flow to be tested successfully.

No additional failures are being invented. These are the meaningful failures that were actually encountered during development.

## AI Interactions

### Evidence E-TEST-01
**SDLC activity:** test

**Task/feature:** Build automated coverage for the implemented bookmark functionality.

**Context given to AI:** The project had the mandatory bookmark requirements and an existing .NET test project. The tests needed to cover service/repository behavior rather than only the UI happy path.

**Prompt/request:** Asked Copilot to implement the required functionality together with appropriate unit tests and verify the test suite.

**AI response summary:** Copilot added unit tests alongside the implementation and reported passing test runs during the incremental build phases.

**Your decision:** Accepted

**What you changed and why:** I reviewed the generated tests and retained the useful coverage while continuing to verify the application behavior manually in the browser.

**How you verified it:** The final local `dotnet test` run passed with 51 tests passed, 0 failed, and 0 skipped.

**Outcome:** worked

**Iteration:** Testing was expanded as additional features such as filtering, search, edit, and delete were implemented.

**Approx. time:** Not separately recorded.

**Learning:** Automated tests are most useful when added with the feature rather than postponed until the end.

### Evidence E-TEST-02
**SDLC activity:** test

**Task/feature:** Investigate the tag-filter test failure for mixed-case tags.

**Context given to AI:** The `Nagarro AI` filter returned no bookmarks even though the bookmark had that tag. The requirement was that tags should be case-insensitive.

**Prompt/request:** Asked Copilot to investigate why the tag filter was not returning the expected bookmark and to trace the behavior through the frontend, API, and stored data.

**AI response summary:** Copilot identified a case-sensitivity mismatch between stored tag values and the filter comparison and changed the comparison to case-insensitive matching.

**Your decision:** Accepted

**What you changed and why:** I accepted the targeted fix because it matched the agreed requirement that tags should not be case-sensitive.

**How you verified it:** The filter was run again with `Nagarro AI`, and the expected bookmark was returned. I confirmed that it was working.

**Outcome:** caused rework

**Iteration:** The failed filter test led to a code change followed by a browser retest.

**Approx. time:** Not separately recorded.

**Learning:** Normalization needs to be consistent at both storage and comparison boundaries.

### Evidence E-TEST-03
**SDLC activity:** test

**Task/feature:** Extend testing as Search, Edit, and Delete were implemented.

**Context given to AI:** The remaining CRUD/search requirements needed automated coverage plus browser smoke testing, including duplicate handling during edit and confirmation for delete.

**Prompt/request:** Asked Copilot to implement the remaining user stories together and add the corresponding tests and smoke checks.

**AI response summary:** Copilot added search, edit, and delete functionality, added 13 unit tests, and reported a final implementation-phase total of 51 passing tests.

**Your decision:** Accepted

**What you changed and why:** I reviewed the implementation and retained the generated coverage, while treating the reported test result as an implementation-phase result that must be repeated locally for the final submission.

**How you verified it:** Search, edit, and delete were smoke-tested during implementation and were also included in the final manual regression. The final automated run passed with 51 tests.

**Outcome:** worked

**Iteration:** The new tests were added after the earlier feature tests and the full suite was rerun.

**Approx. time:** Not separately recorded.

**Learning:** Testing the interaction between features is important; for example, edit must preserve duplicate protection and delete must have a clear safeguard.

## Known Limitations

- A formal performance benchmark at 1,000 bookmarks has not been recorded yet; the current implementation is designed for the local exercise scale.
- The automated suite does not replace full browser regression testing, so the final demo run should recheck all mandatory UI flows.
- The duplicate check has a narrow concurrency limitation: simultaneous Add operations could theoretically pass the duplicate check before either write becomes visible to the other request. This is documented as a known design limitation rather than being presented as solved.

