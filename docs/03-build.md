# Build – Personal Bookmark Manager with Tagging

## Implementation Plan vs. Actual

The implementation was intended to proceed incrementally from the foundation into individual user stories. The planned dependency order was: Foundation/Persistence → Add Bookmark → Tagging → List & View → Filter → Search → Edit → Delete, with validation/error handling and UI wiring applied across the slices.

The actual implementation followed the same dependency-driven principle but changed the order during development based on usability and verification needs:

| Planned area | What was actually built | Deviation / reason |
|---|---|---|
| Foundation / persistence | Built first: layered solution, `Bookmark` model, repository abstraction, JSON persistence with atomic write-to-temp-then-replace, DI wiring, and tests. | No material deviation. |
| User Story 1 – Save a link quickly | Implemented next, including URL validation, normalization, duplicate detection, optional title fetching, title-fetch failure handling, API endpoint, and tests. | No material deviation. |
| Browser UI | A minimal `wwwroot` UI was added immediately after the first backend slice rather than waiting until all backend stories were complete. | Human decision to make each subsequent story visible and runnable in the browser. This made incremental smoke testing easier. |
| User Story 2 – Organize with tags | Tag normalization and tag-related behavior were incorporated into the implementation before/alongside later list/filter work. | The implementation sequence was adjusted while following the dependency graph rather than treating each planning item as a rigid serial step. |
| User Story 4 – List & View | Implemented before the planned explicit Filter slice: `GET /api/bookmarks/`, newest-first ordering, list rendering, and empty state were added and tested. | Building the list first provided the UI surface needed for filtering/search. |
| User Story 3 – Filter by tag | Implemented with tag filtering, virtual `Untagged`, `/api/tags`, UI dropdown, and tests. A real case-insensitive matching bug was then diagnosed and fixed. | Iterative debugging was required after browser verification exposed a data-normalization mismatch. |
| User Story 4b – Search, User Story 5 – Edit, User Story 6 – Delete | Search, Edit, and Delete were implemented together after the user explicitly chose to proceed with the remaining stories in one implementation pass. | This was a deliberate change from one-story-at-a-time implementation after the earlier stories had been exercised in the browser. |
| Cross-cutting validation/security | URL scheme validation, URL normalization, tag normalization, SSRF checks, timeout/response-size limits, duplicate checks, and user-friendly error handling were implemented across the relevant slices. | Implemented alongside features rather than deferred to the end. |

The final implementation remains locally runnable as a browser-based ASP.NET Core application with JSON persistence and the six planned user stories represented in the current codebase.

## Feature Evidence Matrix

The matrix below maps the eleven mandatory functional requirements from the assignment to the implementation journey and verification evidence. The assignment requires one row for each mandatory requirement and the columns shown here. fileciteturn2file0

| Requirement | Status | How AI helped | What I changed/decided | How I verified |
|---|---|---|---|---|
| **Add Bookmark** | Verified | Copilot generated the initial Add pipeline: URL validation, duplicate check, optional title fetch, failure handling, persistence, API endpoint, and unit tests. | Kept the title policy explicit: user title first; otherwise fetch HTML `<title>`; if fetching fails, require a manual title rather than saving `Untitled`. | User tested and verified the User Story 1 acceptance criteria. Copilot also performed an end-to-end browser smoke test. |
| **Tags** | Implemented and unit-tested | Copilot added tag normalization and wired it into the save/update flow; tests cover trim, lowercase, case-insensitive deduplication, blank tags, and length limits. | Tags remain optional. Normalization is trim + lowercase + case-insensitive deduplication. | Unit tests exist for `TagNormalizer`; browser smoke testing also showed `"Demo, DEMO, reading"` becoming `demo, reading`. |
| **List / Newest First** | Verified | Copilot added `GET /api/bookmarks/`, newest-first sorting, list rendering, and empty-state handling, plus tests. | Kept list sorting based on `CreatedAt` and used the existing JSON-backed repository. | Copilot ran 38 tests and browser smoke-tested the list; a newly added bookmark appeared first. |
| **Filter by Tag** | Verified after fix | Copilot added tag query filtering, `/api/tags`, the filter dropdown, `Untagged`, tests, and investigated the failing `Nagarro AI` filter case. | Corrected the filter comparison to `StringComparer.OrdinalIgnoreCase` after identifying the mismatch between older persisted tag casing and normalized filter input. | User reported the `Nagarro AI` filter failure; after the code fix and restart, user confirmed: **"Okay working now."** |
| **Search** | Verified in final manual regression | Copilot added case-insensitive title/URL search, debouncing, endpoint query support, UI search, and tests. | Kept search as a server-side filter over title and URL and combined it with the existing list/filter flow. | Copilot browser smoke test filtered a bookmark by title text. The final manual regression also passed for this flow. |
| **Edit** | Verified in final manual regression | Copilot added `UpdateBookmarkRequest`, `UpdateAsync`, `PUT /api/bookmarks/{id}`, edit mode, duplicate exclusion, and tests. | Preserved `CreatedAt`, updated `UpdatedAt`, and re-used the shared validation flow. | Copilot browser smoke test changed a title and saved successfully. Final user verification passed during the manual regression. |
| **Delete** | Verified in final manual regression | Copilot added `DeleteAsync`, `DELETE /api/bookmarks/{id}`, confirmation dialog, UI refresh, and tests. | Kept confirmation before permanent deletion and persisted the removal through the repository. | Copilot browser smoke test confirmed the dialog and then verified deletion through the API. Final user verification passed during the manual regression. |
| **Persistence** | Implemented and verified in final manual regression | Copilot generated the JSON repository, in-memory cache, atomic temp-file replacement, and persistence/reload tests. | Chose local JSON storage instead of an external database; kept atomic write-to-temp-then-replace. | Repository tests cover disk persistence and reload using a new repository instance. Final stop/restart browser verification passed. |
| **Validation** | Implemented and unit-tested | Copilot added URL scheme/length validation, normalization, tag validation, title-fetch failure handling, and user-facing error responses. | Restricted URLs to HTTP/HTTPS and rejected invalid input before persistence. | Unit tests cover invalid/disallowed URLs, URL length, tag length, title-fetch failure, and no persistence on validation failure. User also tested invalid URL behavior during Story 1 verification. |
| **Duplicate Handling** | Implemented and unit-tested; concurrency race remains documented | Copilot added normalized URL duplicate checks and self-exclusion during edit. | Accepted normalized duplicate detection, while the design review documented that the check-then-add sequence still has a narrow simultaneous-request race window. | Unit tests cover duplicate normalized URLs and edit-to-own-URL behavior. The design also documents the unresolved concurrency limitation honestly. |
| **Empty / Error States** | Implemented; verified in final manual regression | Copilot added empty list, no-match search/filter, title-fetch failure, invalid-input, server-error, and delete-error feedback. | Kept first-run empty state distinct from filtered/search no-match state and used plain-language messages. | Unit tests cover empty/no-match cases; browser smoke tests exercised list/filter/search/error flows. The final manual walkthrough passed for the main states. |

## Troubleshooting

### Problem 1 – Tag filter failed for `Nagarro AI`

**What happened:** After the tag filter was implemented, the tag dropdown contained `Nagarro AI`, but selecting it returned **"No bookmarks match your filter"** even though the bookmark existed.

**Diagnosis:** Copilot inspected `BookmarkService.cs`, the persisted `bookmarks.json`, and the frontend/API behavior. It identified that the older persisted bookmark contained the tag as `"Nagarro AI"`, while the filter path was comparing a lowercased value against the stored value using a case-sensitive comparison.

**Resolution:** `BookmarkService` was changed to compare tags using `StringComparer.OrdinalIgnoreCase`.

**Verification:** Copilot rebuilt/restarted the application and re-tested the filter. The user then confirmed that it was working: **"Okay working now."**

**Why this matters:** This was a real implementation defect caused by data created before tag normalization existed. It was not merely a documentation issue.

### Problem 2 – Browser showed stale/missing tag-filter data

**What happened:** The user ran the application on `http://localhost:5164` and did not see the expected tag options or bookmarks, even though the server/API behavior was correct.

**Diagnosis:** Copilot checked the running process, `launchSettings.json`, `index.html`, `app.js`, the API responses, and the exact file served by the application. It confirmed that `/api/tags` and `/api/bookmarks/` returned the expected data and that the current `app.js` contained the tag-filter code. It identified a stale browser-cached copy of the static files as the likely cause.

**Resolution:** Copilot recommended a hard refresh or an Incognito window and then continued investigating the actual filter behavior. During this investigation it also removed a harmless duplicate `setMessage` function from `app.js`.

**Verification:** Copilot confirmed the server was serving the current `app.js` and the correct API responses. The later `Nagarro AI` issue was a separate real application bug and was fixed independently.

### Additional implementation issue – stale application process / file lock

During the List and Search/Edit/Delete implementation, Copilot encountered an existing `BookmarkManager.Api` process holding the build output DLL. It terminated the stale process, rebuilt, restarted the application, and continued browser smoke testing. This was an environment/process issue rather than an application defect.

## Significant Human Changes

1. **Chose incremental browser UI instead of waiting until all backend stories were complete.** Copilot initially suggested finishing backend stories first, but I chose to add a minimal `wwwroot` UI immediately so each feature could be exercised through the browser as it was built. This reduced the risk of discovering UI/API integration problems only at the end.

2. **Defined the final title-failure behavior.** I explicitly decided that an automatically fetched title failure must not result in an `Untitled` bookmark. The user must provide a title before the bookmark is saved. Copilot updated the planning/acceptance criteria and implementation followed that rule.

3. **Chose JSON persistence and accepted its trade-offs.** I selected a local JSON file-backed repository instead of SQLite/EF Core because the assignment is local and does not require an external database. The implementation uses an in-memory cache plus atomic file replacement. The design documents the trade-off and the remaining concurrency limitation rather than hiding it.

4. **Corrected AI output when it overstated the design.** During the design audit, I accepted corrections that removed unsupported claims about a tag index, pagination, a title-length cap, and formal Strategy/Chain of Responsibility/Template Method implementations. The documentation was changed to match the actual code rather than changing code solely to satisfy the earlier AI-generated description.

5. **Accepted a targeted bug fix after inspecting the evidence.** For the `Nagarro AI` filter issue, the problem was diagnosed as case-sensitive comparison against older persisted data. I accepted the `OrdinalIgnoreCase` fix and then verified that the filter worked.

## AI Interactions

### Evidence E-BUILD-01
**SDLC activity:** build

**Task/feature:** Foundation and User Story 1 – Save a link quickly

**Context given to AI:** The planned architecture used separate Core, Persistence, API, and Tests projects. User Story 1 required URL validation, optional title fetching, duplicate detection, persistence, and a browser-runnable API.

**Prompt/request:** Build the foundation and User Story 1 incrementally, including the repository, validation/title-fetch pipeline, API endpoint, and tests.

**AI response summary:** Copilot created the layered solution, `Bookmark` model, repository abstraction and JSON repository, URL validation/normalization, title-fetch components, `BookmarkService.AddAsync`, `POST /api/bookmarks`, and unit tests. It reported 28 tests passing and smoke-tested valid save, duplicate rejection, and `javascript:` rejection.

**Your decision:** Accepted, with the title-failure behavior constrained by the planning decision that no `Untitled` placeholder should be persisted.

**What you changed and why:** I reviewed the generated implementation and tested the User Story 1 acceptance criteria. I retained the local JSON architecture and the explicit manual-title fallback requirement.

**How you verified it:** I tested and verified the User Story 1 acceptance criteria. Copilot also performed API/browser smoke tests and the generated unit tests covered the main Add pipeline cases.

**Outcome:** Worked for the verified User Story 1 scenarios.

**Iteration:** The implementation was followed by browser UI work so the feature could be exercised through the actual web application.

**Approx. time:** Copilot interaction duration for the later browser-UI slice is recorded separately; total build time for this story is not established in the available history.

**Learning:** Build the core validation/persistence pipeline first, but verify it through the real browser flow rather than relying only on unit tests.

### Evidence E-BUILD-02
**SDLC activity:** build

**Task/feature:** Incremental browser UI for the Add Bookmark flow

**Context given to AI:** The backend could save bookmarks, but there was no browser UI yet. The design called for a local web application accessible through a browser.

**Prompt/request:** Build a minimal browser UI now and grow it incrementally with each user story rather than waiting until all backend stories were finished.

**AI response summary:** Copilot added `wwwroot/index.html`, `app.js`, and `styles.css`, wired static-file serving, connected the form to the API, opened the application in a browser, and successfully saved a bookmark through the UI.

**Your decision:** Accepted.

**What you changed and why:** I chose incremental UI delivery so every subsequent story could be demonstrated and smoke-tested in the browser.

**How you verified it:** Copilot opened the local page and completed an Add Bookmark flow, reporting `Saved "Example Domain".` and a form reset.

**Outcome:** The application became browser-runnable before all user stories were complete.

**Iteration:** The same page was later extended for list, filter, search, edit, and delete rather than introducing a separate frontend application.

**Approx. time:** Copilot reported 19 steps in approximately 2 minutes 18 seconds for this interaction; total user activity time is not established.

**Learning:** A small same-origin UI early in development makes API and UX integration issues visible sooner.

### Evidence E-BUILD-03
**SDLC activity:** build

**Task/feature:** User Story 4 – List & View Bookmarks

**Context given to AI:** The Add flow worked, and the next usable browser capability was to retrieve and display saved bookmarks.

**Prompt/request:** Implement the List & View story with a GET endpoint, newest-first ordering, tests, and browser UI rendering.

**AI response summary:** Copilot added `GET /api/bookmarks/`, newest-first sorting, `GetAllAsync` coverage, list rendering, empty-state handling, and styling. It ran the test suite and reported 38 tests passing, then browser-smoke-tested the list and newest-first behavior.

**Your decision:** Accepted.

**What you changed and why:** I kept the list as a server-backed view of the persisted bookmarks and continued using the existing page rather than introducing a second UI.

**How you verified it:** Copilot confirmed that existing persisted data rendered and that a newly added bookmark appeared first. I also tested the relevant user-story behavior during the incremental verification process.

**Outcome:** List and newest-first behavior worked in the browser smoke test.

**Iteration:** The list became the foundation for the later tag filter and search experiences.

**Approx. time:** Copilot reported 27 steps in approximately 3 minutes 6 seconds; total user activity time is not established.

**Learning:** Implementing the read/list surface before filtering made later filter and search features easier to integrate and verify.

### Evidence E-BUILD-04
**SDLC activity:** build

**Task/feature:** Fix tag filtering for a persisted tag containing spaces and mixed case (`Nagarro AI`)

**Context given to AI:** The tag dropdown showed the tag, but selecting `Nagarro AI` returned no bookmarks even though the bookmark existed.

**Prompt/request:** Investigate why filtering by `Nagarro AI` returned no results and fix the issue.

**AI response summary:** Copilot inspected `BookmarkService.cs` and `bookmarks.json`, traced the API/filter behavior, and found that older persisted data contained `Nagarro AI` while the filter comparison was case-sensitive. It changed the comparison to `StringComparer.OrdinalIgnoreCase`.

**Your decision:** Accepted the diagnosis and targeted fix.

**What you changed and why:** I accepted the minimal comparison fix because it matched the intended case-insensitive tag requirement and also handled older persisted data created before normalization existed.

**How you verified it:** Copilot rebuilt/restarted the API and re-tested the filter. I then confirmed that the filter worked: **"Okay working now."**

**Outcome:** The previously failing `Nagarro AI` filter worked after the fix.

**Iteration:** This issue also reinforced the need for consistent normalization and case-insensitive matching across newly created and previously persisted data.

**Approx. time:** Copilot reported 8 steps in approximately 47 seconds; total troubleshooting time is not established.

**Learning:** Persisted data can outlive an implementation rule. Read/filter logic should remain robust when older data does not follow the latest normalization convention.

### Evidence E-BUILD-05
**SDLC activity:** build

**Task/feature:** Search, Edit, and Delete

**Context given to AI:** After the earlier stories had been exercised, I explicitly asked to implement the remaining user stories in one pass.

**Prompt/request:** Continue and implement the remaining user stories: Search, Edit, and Delete.

**AI response summary:** Copilot added search by title/URL, edit request/service/API/UI support, duplicate exclusion during edit, delete service/API/UI support, confirmation dialog, tests, and browser smoke tests.

**Your decision:** Accepted the implementation direction, while retaining the existing design-document corrections that avoid overstating formal design patterns.

**What you changed and why:** I chose to implement the remaining stories together after the earlier incremental browser verification had established the main architecture and integration path.

**How you verified it:** Copilot browser-smoke-tested search, edit, and delete. It reported 13 new unit tests and 51 total tests passing at that point. Final user-run regression verification was subsequently completed successfully.

**Outcome:** Copilot's end-to-end smoke tests showed Search, Edit, and Delete working together in the browser.

**Iteration:** The implementation was followed by the final testing/review phase rather than further feature expansion.

**Approx. time:** Copilot reported 44 steps in approximately 8 minutes 46 seconds; total user activity time is not established.

**Learning:** Once the core API and list UI were stable, the remaining CRUD interactions could share validation, persistence, and the same browser surface without creating another architectural layer.

### Evidence E-BUILD-06
**SDLC activity:** build

**Task/feature:** Diagnose missing tag options/bookmarks in a browser instance

**Context given to AI:** The application appeared correct on the server, but the browser at `http://localhost:5164` did not show the expected tag options or bookmarks.

**Prompt/request:** Investigate why the browser instance did not display the current tag-filter UI/data.

**AI response summary:** Copilot inspected the running process, launch settings, HTML/JS files, API responses, and the exact static `app.js` served by the application. It found the API returned the expected bookmarks/tags and the server was serving current JavaScript, then identified stale browser caching as the likely cause and suggested a hard reload/Incognito window. It also removed a harmless duplicate `setMessage` function.

**Your decision:** Accepted the diagnosis and the cache-refresh approach; the later application-level `Nagarro AI` failure was investigated separately.

**What you changed and why:** I followed the cache-refresh/restart approach and continued testing the filter. The separate case-insensitive comparison bug was then fixed when the actual application failure was reproduced.

**How you verified it:** Copilot confirmed the API responses and served static file contents. The subsequent filter test isolated the remaining issue as the tag-comparison bug documented in E-BUILD-04.

**Outcome:** The stale-browser issue was separated from the actual tag-filter defect, preventing the wrong component from being changed.

**Iteration:** Investigation continued until the real application-level defect was reproduced and fixed.

**Approx. time:** Copilot reported 13 steps in approximately 3 minutes 37 seconds; total troubleshooting time is not established.

**Learning:** When UI behavior disagrees with API behavior, verify the exact served static assets and API responses before changing application logic.

## Local Run Evidence

**Application Running**

The application was run locally using the configured .NET application.
![Application running locally](screenshots\app-running.png)

**Add Bookmark**
A bookmark was successfully added with title and tags.
![Add1](screenshots\add-1.png)
![Add2](screenshots\add-2.png)

**Search and Filter**
Search by title/URL and filtering by tag were verified.
![Filter](screenshots\filter.png)
![Search](screenshots\search.png)


**Edit and Delete**

Edit and delete functionality was verified from the UI.

![Edit](screenshots\edit.png)
![Delete 1](screenshots\delete-1.png)
![Delete 2](screenshots\delete-2.png)

**Persistence**

The application was restarted and previously saved bookmarks were still available.

![Persistence after restart](screenshots\persistance.png)



### Verification completed during implementation

- Browser page loaded successfully with the Add Bookmark UI.
- Add Bookmark was exercised through the browser.
- List rendering and newest-first behavior were browser-smoke-tested.
- Tag filtering, including the `Nagarro AI` failure and subsequent fix, was browser-tested.
- Search, Edit, and Delete were browser-smoke-tested by Copilot.
- JSON persistence was covered by repository tests and should be demonstrated again through a final application stop/restart before submission.


### Final verification completed

The implementation history contains AI browser smoke tests and several user confirmations. The final local regression covered the following areas and all passed:

- `dotnet build`
- `dotnet test` and record the actual final test count/result
- Add bookmark with explicit title
- Add bookmark without title and verify automatic HTML title retrieval
- Trigger title-fetch failure and verify manual-title prompt / no `Untitled` persistence
- Invalid URL validation
- Duplicate URL handling
- Tag normalization and filtering, including a tag containing spaces
- `Untagged` filter
- Search by title and URL
- Edit URL/title/tags
- Delete confirmation, cancel, and confirm
- Stop and restart the application and verify persistence
- Confirm empty-list and no-match states

Final local verification on the user's development machine completed successfully: `dotnet build` passed, `dotnet test` passed with 51 tests passed, 0 failed, 0 skipped, and the final manual regression passed for Add, Search / Filter, Edit, Delete, and Persistence.
