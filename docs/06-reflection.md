# 06-reflection

## AI Usage Summary

AI was used across the major SDLC phases rather than only for code generation.

- **Planning:** AI helped break down the requirements, clarify title-fetch behavior, identify edge cases, and refine acceptance criteria. Important decisions included requiring a user-provided title when automatic title retrieval fails, case-insensitive tag handling, and the virtual `Untagged` filter.
- **Design:** AI helped evaluate local JSON persistence, project structure, error handling, and possible design patterns. The final design was kept simple and was checked against the actual implementation.
- **Build:** AI assisted with the layered .NET implementation, CRUD functionality, UI, validation, persistence, search, filtering, edit, and delete flows.
- **Testing:** AI helped identify additional edge cases and investigate failures. The tag-filter case-sensitivity issue and browser/process-related troubleshooting were particularly useful examples.
- **Review:** AI was used to inspect correctness, security, validation, accessibility, maintainability, and UX considerations. Suggestions were evaluated against the actual source rather than accepted blindly.
- **Documentation:** AI helped organize the SDLC evidence into the required Markdown structure.

The most useful aspect was having AI review requirements and implementation from different perspectives. The least useful aspect was accepting high-level pattern recommendations before checking whether the actual implementation genuinely used those patterns. This was corrected through source inspection.

## Approximate SDLC Time

The exact total time was not recorded consistently during the exercise, so the following is an approximate reflection rather than a measured timesheet.

| SDLC Phase | Approximate Time |
|---|---|
| Planning | 1–2 hours |
| Design | 1–2 hours |
| Build | 4–6 hours |
| Testing / Troubleshooting | 2–3 hours |
| Review / Documentation | 2–3 hours |
| **Total** | **Approximately 10–16 hours** |

These are estimates based on the overall work and should not be interpreted as exact tracked durations.

## Rework

AI saved time by accelerating boilerplate implementation, generating supporting tests, explaining unfamiliar implementation details, and helping investigate errors.

AI also created some additional review work. For example:

1. Some design-pattern recommendations were broader than what the final source actually implemented. I had to inspect the source and remove unsupported pattern claims from the design documentation.
2. The tag filtering implementation initially had a case-sensitivity issue. The problem was diagnosed through browser/API/source inspection and then corrected.
3. Browser caching and a stale application process caused additional troubleshooting before the correct application state could be verified.

The main lesson was that AI output reduced implementation effort, but verification remained necessary.

## Human Judgment

### 1. Title behavior

I decided that the title should remain optional for the user, but the application should attempt to retrieve the HTML page title when the field is blank. If retrieval fails, the user should be asked to provide a title rather than saving a generic `Untitled` value.

This decision kept the saved data meaningful and matched the intended user experience.

### 2. Persistence approach

I chose local JSON persistence instead of introducing an external database. The application keeps the data in memory during normal operation and persists changes to a JSON file, including an atomic temporary-file-and-rename approach.

This was appropriate for the scope and local-running constraint of the exercise.

### 3. Tag behavior

I decided that tags should be case-insensitive and normalized so that values such as `Nagarro AI` and `nagarro ai` are treated consistently. I also decided that `Untagged` should be a virtual filter representing bookmarks with no tags rather than a persisted tag value.

These decisions were based on expected usability and the requirements, rather than simply accepting an AI-generated implementation.

## What You Would Do Differently

If I repeated the exercise, I would:

1. **Record AI evidence immediately** after each important interaction instead of reconstructing some details later.
2. Give AI more explicit constraints earlier, especially around which design patterns were actually required versus merely possible.
3. Run focused tests after each feature before moving to the next feature, particularly for filtering and UI behavior.
4. Capture screenshots during verification rather than collecting all evidence near the end.
5. Maintain a small verification checklist throughout the build so that final regression testing is faster.
6. Ask AI for targeted reviews with the current source code and exact requirements instead of relying on broad architectural recommendations.

## Self-Assessment

**Strongest phase:** Build and troubleshooting. AI was particularly useful for implementing features incrementally and investigating concrete problems such as tag filtering and stale application/browser state.

**Weakest phase:** Final verification/documentation tracking. Some execution details and exact timings were not captured at the moment they occurred, which made the final documentation more dependent on available history.

**Confidence in the result:** Good. The implementation has been reviewed against the requirements, the final local `dotnet build` passed, all 51 automated tests passed, and the final manual regression passed for Add, Search / Filter, Edit, Delete, and Persistence.

## Demo Video

**Filename / Link:** `<Add demo video link here>`

**Duration:** `<5–8 minutes>`

The demo will cover:

1. Running the application locally.
2. Adding a bookmark with tags.
3. Listing bookmarks in newest-first order.
4. Searching by title/URL.
5. Filtering by tag, including the `Untagged` case.
6. Editing a bookmark.
7. Deleting a bookmark with confirmation.
8. Validation and an edge/error case.
9. Restarting the application and demonstrating persistence.
10. One useful AI interaction and how the suggestion was evaluated.
11. The tag-filter problem and how AI helped diagnose and resolve it.
12. Where AI was useful and where human judgment was required.

The assignment requires a 5–8 minute technical demonstration covering these areas. 

## Declaration

I confirm that this documentation is intended to reflect my actual work, decisions, verification activities, and AI-assisted interactions during the Personal Bookmark Manager exercise.

I have not intentionally fabricated AI interactions, test failures, test results, or verification outcomes. Where exact timing or final verification data was not recorded, it is identified as an estimate or left for final verification rather than presented as an exact result.
