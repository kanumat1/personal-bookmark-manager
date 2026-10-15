# Planning – Personal Bookmark Manager with Tagging

## Problem Understanding

People constantly collect useful links while researching or browsing — articles, docs,
tutorials, tools — but browser bookmarks are hard to organize, search, and tag, and they
don't travel well across contexts. This project is a small, self-hosted web application
that lets a single user save URLs, auto-fetch a page title when one isn't supplied, tag
bookmarks for organization, and later find them again by tag or free-text search.

- **Target user**: An individual (developer, researcher, student) who wants a lightweight,
  local, no-account bookmarking tool instead of relying on browser bookmark bars or paid
  SaaS bookmarking services.
- **Value delivered**: Faster retrieval of previously-found resources through tagging and
  search, reduced friction when saving (title auto-fill), protection from accidental data
  loss (delete confirmation, duplicate detection), and durability of the data across
  restarts (local persistence) — all without needing an internet-hosted backend or paid
  infrastructure.
- **Scope boundaries**: Single-user, runs locally, web UI in a browser, no external
  database or cloud dependency required. Technology stack is flexible; the plan below
  assumes a .NET 8 backend (ASP.NET Core Web API/Razor) with a local JSON file-based
  store and a browser-based front end, since that fits the "runnable locally, no paid
  cloud" constraint well.

## Requirement Breakdown

Work is split into vertical feature slices, each with the tasks needed to deliver it, plus
the dependencies between slices.

1. **Foundation / Persistence layer** *(no dependencies — build first)*
   - Define `Bookmark` data model (Id, Url, Title, Tags, CreatedAt, UpdatedAt).
   - Implement a JSON file-backed repository (`bookmarks.json`): load into memory on
     startup, write back on every create/update/delete using an atomic
     write-to-temp-then-rename pattern to avoid partial/corrupt writes.
   - Implement repository/service layer (CRUD) behind an interface, with unit tests.
2. **Add Bookmark** *(depends on #1)*
   - URL input + optional title field.
   - URL validation (format check, reachability optional).
   - Title-fetch service: triggered only at save time, and only when the user left the
     title blank. HTTP GET the URL, parse `<title>`, timeout/fallback handling. If the
     fetch fails (or yields no usable title), the save is blocked and the user is
     prompted to manually enter a title — no automatic "Untitled" placeholder is ever
     persisted.
   - Duplicate URL detection (normalize URL, check existing records) before save.
   - Persist bookmark with tags.
3. **Tagging** *(depends on #1, informs #2 and #4)*
   - Allow free-text, comma-separated (or chip-style) tag entry.
   - Normalize tags (trim, case-insensitive, dedupe).
   - Store tags as a normalized list on each bookmark; a bookmark can have multiple tags, and the same tag can be used by multiple bookmarks.
4. **List & View Bookmarks** *(depends on #1)*
   - Retrieve all bookmarks, sorted newest-first.
   - Display empty state when no bookmarks exist.
5. **Filter by Tag** *(depends on #3, #4)*
   - Tag list/dropdown of all existing tags, plus a built-in virtual "Untagged" tag that
     always appears (even if no bookmarks currently lack tags).
   - Filter bookmark list by selected tag; selecting "Untagged" shows only bookmarks with
     zero tags; empty state when none match.
6. **Search** *(depends on #4)*
   - Search box matching title or URL (substring / case-insensitive).
   - Empty state when no results match.
7. **Edit Bookmark** *(depends on #2, #3)*
   - Load existing bookmark into an edit form; allow URL/title/tag changes.
   - Re-validate URL and re-check duplicates on save (excluding itself).
   - Persist the updated bookmark to `bookmarks.json` (atomic write) so edits survive
     a restart.
8. **Delete Bookmark** *(depends on #4)*
   - Confirmation dialog/step before delete (safeguard against accidental loss).
   - Remove bookmark and its tag associations from the in-memory store and persist the
     change to `bookmarks.json` (atomic write) so the deletion survives a restart.
9. **Validation & Error/Empty-State Handling** *(cross-cutting, layered on #2, #5, #6, #8)*
   - Central validation for empty/invalid URLs with user-friendly messages; invalid input
     is rejected before it reaches the repository layer and is never written to
     `bookmarks.json`.
   - Duplicate-URL detection (from #2/#7) surfaces a clear, user-friendly message (e.g.
     "This URL is already saved") instead of a generic error or silent no-op.
   - Title-fetch failure handling: if automatic title retrieval fails, the app prompts the
     user to manually enter a title and blocks the save until one is provided; it never
     substitutes a placeholder like "Untitled".
   - Consistent empty-state and error UI components reused across list/search/filter.
10. **UI polish & wiring** *(depends on all above)*
    - Single-page or multi-view web UI tying add/list/filter/search/edit/delete together.
    - Loading/error indicators.

Dependency summary: `1 → (2,4)`, `1 → 3`, `(3,4) → 5`, `4 → 6`, `(2,3) → 7`, `4 → 8`,
validation/error-handling threads through 2, 5, 6, 7, 8; UI wiring (10) depends on
everything.

## User Stories & Acceptance Criteria

1. **Save a link quickly**
   - *As a* researcher, *I want* to optionally leave the title blank and have the app try
     to fetch one for me when I save, *so that* I don't have to type a title myself unless
     the app truly can't find one.
   - Acceptance criteria:
     - Given a valid URL with a title I typed myself, when I save, the bookmark is stored
       with exactly the title I provided (no fetch is attempted).
     - Given a valid URL with no title typed, when I save, the app attempts to fetch the
       page's `<title>` as part of the save operation and stores that title if found.
     - If the title cannot be fetched (timeout, 404, non-HTML response) and none was typed,
       the bookmark is **not** saved with a placeholder like "Untitled"; instead the app
       shows an error and explicitly asks the user to type a title before the save can
       complete.
     - An empty or malformed URL (e.g. `not a url`, empty string) is rejected with a clear
       inline error message and nothing is saved.

2. **Organize with tags**
   - *As a* user with many bookmarks, *I want* to assign one or more tags to a bookmark,
     *so that* I can group related resources together.
   - Acceptance criteria:
     - I can add one or multiple tags when creating or editing a bookmark.
     - Tags are trimmed and deduplicated case-insensitively (e.g. "Work", "work", " work "
       are treated as the same tag).
     - A bookmark can be saved with zero tags (tags are optional, not required).

3. **Find bookmarks by tag**
   - *As a* returning user, *I want* to filter my bookmarks by a specific tag, including a
     built-in "Untagged" option, *so that* I can quickly see all resources related to one
     topic or find the ones I haven't organized yet.
   - Acceptance criteria:
     - Selecting a tag shows only bookmarks that have that tag, sorted newest-first.
     - A built-in "Untagged" tag is always available in the tag list/dropdown and, when
       selected, shows only bookmarks that currently have zero tags, sorted newest-first.
     - Selecting a tag (or "Untagged") with no associated bookmarks shows a friendly "no
       bookmarks found" message rather than an error or blank screen.
     - Clearing the filter returns to the full, unfiltered list.

4. **Search across title and URL**
   - *As a* user who half-remembers a link, *I want* to search by keyword across titles and
     URLs, *so that* I can find a bookmark without remembering its exact tag or title.
   - Acceptance criteria:
     - Entering a search term returns bookmarks whose title or URL contains that term
       (case-insensitive).
     - A search with no matches shows a clear "no results" state, not an error.
     - Clearing the search box restores the full bookmark list.

5. **Edit an existing bookmark**
   - *As a* user who made a mistake or wants to reorganize, *I want* to edit a bookmark's
     URL, title, or tags, *so that* my collection stays accurate over time.
   - Acceptance criteria:
     - I can open an existing bookmark, change any of URL/title/tags, and save.
     - Saving re-validates the URL and re-checks for duplicates (a bookmark can be edited
       to match its own original URL without triggering a false duplicate error).
     - Changes persist and are reflected immediately in the list.

6. **Delete without accidents**
   - *As a* user cleaning up my list, *I want* to delete a bookmark with a confirmation
     step, *so that* I don't accidentally lose a resource I still need.
   - Acceptance criteria:
     - Clicking delete prompts a confirmation (dialog or undo window) before the bookmark
       is permanently removed.
     - Confirming deletes the bookmark and its tag associations; canceling leaves it
       untouched.
     - After deletion, the list updates immediately and reflects the change after a page
       refresh (i.e. persisted).

## Edge Cases

At least six relevant edge cases identified, with a note on how AI assistance helped
surface or refine them:

1. **Invalid or empty URL** (e.g. `""`, `"javascript:alert(1)"`, `"htp:/bad"`) — must be
   rejected with a clear message and never persisted. *AI-assisted*: AI flagged
   `javascript:`/`data:` URL schemes as an XSS/security concern in addition to plain
   format validation, which wasn't in the original written requirement.
2. **Duplicate URL** — saving a URL that (after normalization: trimming trailing slash,
   lowercasing scheme/host) already exists should be detected and surfaced rather than
   silently creating a second row. *AI-assisted*: AI suggested normalizing URLs before
   comparison (trailing slash, `http` vs `https`, query string ordering) instead of a naive
   exact-string match, since users often paste visually-different-but-same URLs.
3. **Title-fetch failure** — target site is down, times out, blocks bots (403), or returns
   non-HTML content (e.g. a PDF or image link) — the app must not silently save a
   placeholder title; it prompts the user to manually enter a title and only saves once
   one is provided.
4. **No bookmarks saved yet** — first-run/empty state must show a helpful message rather
   than a blank page or error.
5. **No search/filter results** — a search term or tag with zero matches must show a
   friendly empty state distinct from "no bookmarks at all".
6. **Editing a bookmark to its own unchanged URL** — must not be flagged as a duplicate of
   itself. *AI-assisted*: this "self-duplicate" false positive was pointed out by AI review
   of the duplicate-check logic and wasn't in the original acceptance criteria.
7. **Very long URLs, titles, or tag lists** — extremely long input should not break the UI
   layout or storage (define reasonable max lengths).
8. **Concurrent-ish edits / rapid double-submit** — clicking "Add" twice quickly (e.g. slow
   network to fetch title) shouldn't create two identical bookmarks. *AI-assisted*: AI
   raised this double-submit race condition as a likely bug source given the async
   title-fetch step.
9. **Malicious/unsafe input in title or tags** — a fetched page title or user-typed tag
   containing HTML/script content must be safely encoded when rendered (XSS prevention).

## Non-Functional Requirements

- **Performance**: Listing and search/filter operations return results in **under 500 ms**
  for a dataset of **1,000 bookmarks** on typical local hardware (measured server-side
  processing time, excluding network/browser render).
- **Reliability & persistence**: Bookmarks (and their tags) survive an application restart
  or machine reboot with **zero data loss** for successfully-saved records; the JSON store
  is written durably on each create/update/delete via an atomic write (write to a temp
  file, then rename) so a crash mid-write cannot corrupt existing data (no in-memory-only
  store).
- **Availability of local app**: Application starts and becomes usable (list loads) within
  **5 seconds** of launch on a typical developer machine.
- **Usability**: Core actions (add, search, filter, edit, delete) are each reachable within
  **2 clicks/interactions** from the main list view; all empty/error states use plain,
  non-technical language.
- **Accessibility**: UI meets baseline WCAG 2.1 AA practices — all interactive controls are
  keyboard-navigable, form inputs have associated labels, color contrast ratio ≥ 4.5:1 for
  text, and error messages are exposed to screen readers (e.g. `aria-live` regions).
- **Security**: 
  - All user-supplied and fetched content (titles, tags, URLs) is HTML-encoded on output
    to prevent XSS.
  - URLs are restricted to `http`/`https` schemes only (reject `javascript:`, `data:`,
    `file:`, etc.).
  - Outbound title-fetch requests use a bounded timeout (e.g. 5 seconds) and a capped
    response size to avoid resource-exhaustion from malicious/huge responses.
  - No secrets, API keys, or credentials are required or stored, since no external paid
    service is used.
- **Maintainability**: Core domain logic (validation, duplicate detection, title fetching)
  is covered by automated unit tests; persistence layer is swappable behind an interface
  to allow testing without a real database.

## Risks, Assumptions & Questions

**Risks**
- Fetching an external page's `<title>` introduces a network dependency and potential
  latency/timeouts, security exposure (SSRF-style requests to internal addresses if not
  guarded), and parsing fragility (malformed HTML, redirects, non-UTF8 encodings).
- Duplicate detection based on normalized URLs may produce false positives/negatives if
  normalization rules are too aggressive or too lax (e.g. tracking query params like
  `?utm_source=` making genuinely-same links look different).
- Single-user, local-only design means no authentication is planned; if the app is ever
  exposed beyond localhost, this becomes a security gap.
- JSON file-based storage is simple but has no built-in transactional guarantees; a crash
  or concurrent write mid-save could corrupt the file if not written atomically, and the
  whole file must be read/rewritten on each change, which won't scale indefinitely.

**Assumptions**
- Single user, single machine, no multi-user auth or roles required.
- "No paid cloud service or external database" allows use of a local flat-file JSON store
  (embedded in the app, not a separately-hosted service).
- Browser-based UI can be a server-rendered app (e.g. ASP.NET Core Razor Pages/MVC) or a
  small SPA calling a local API; either satisfies "web-based UI accessible through a
  browser."
- "Reasonable confirmation or safeguard" for delete can be satisfied with a confirm
  dialog (no need for full undo/trash-bin history) unless otherwise specified.
- Network access is available for title-fetching, but the app must still function (in a
  degraded way) if it is not.

**Open Questions**
- Should duplicate URLs be strictly blocked, or allowed with a warning (user override)?
- Is multi-tag AND/OR filtering needed (filter by one tag only vs. combining multiple
  tags), or is single-tag filtering sufficient for v1?
- Should deleted bookmarks be soft-deleted (recoverable) or hard-deleted permanently?
- What is the expected max realistic bookmark count for performance targets beyond the
  1,000-record benchmark (e.g. 10,000+)?
- Is there a need to export/import bookmarks (e.g. from browser bookmark files) in scope,
  or is manual entry only in scope for this assignment?

## AI Interactions

### Evidence E-PLAN-01
**SDLC activity:** Planning — clarifying a proposed feature's behavior before writing requirements
**Task/feature:** Title-fetch service (automatic HTML `<title>` retrieval when no title is supplied)
**Context given to AI:** The in-progress planning draft describing the Add Bookmark feature and its title-fetch step
**Prompt/request:** "What does the title-fetch service does?"
**AI response summary:** Copilot explained that when the user leaves the title blank, the service retrieves the page, parses the HTML `<title>` element, and handles timeout, unreachable-host, non-HTML-response, and no-title-found cases gracefully rather than failing silently.
**Your decision:** Accepted the explanation as an accurate description of the intended behavior; no correction was requested.
**What you changed and why:** This interaction was a clarifying question rather than a change request — no planning.md edit resulted directly from it.
**How you verified it:** Not established in the available Copilot history; verified separately during implementation/testing.
**Outcome:** Confirmed shared understanding of the title-fetch service's intended behavior before it was written into Requirement #2 and the related acceptance criteria/edge cases.
**Iteration:** Single turn — question and explanation, no follow-up correction.
**Approx. time:** Not established in the available Copilot history.
**Learning:** Asking Copilot to explain a proposed feature back in plain terms before requirements are finalized is a low-cost way to confirm the behavior is understood the same way by both sides.

### Evidence E-PLAN-02
**SDLC activity:** Planning — requirement refinement / policy decision
**Task/feature:** Add Bookmark — title handling when auto-fetch fails
**Context given to AI:** The in-progress planning draft covering the Add Bookmark requirement and title-fetch behavior
**Prompt/request:** User specified that the title is optional; if not provided, the app should fetch the HTML title, and if that fetch fails, the app should ask the user for a title rather than saving a bookmark titled "Untitled".
**AI response summary:** Copilot updated the planning document to reflect this policy: Requirement #2 (Add Bookmark) states the fetch is attempted only when the title is blank and that a failed fetch blocks the save and prompts for manual entry; Requirement #9 (Validation & Error/Empty-State Handling) states the app never substitutes a placeholder like "Untitled"; User Story 1's acceptance criteria state the bookmark is **not** saved with a placeholder and the app explicitly asks the user to type a title; Edge Case #3 documents the title-fetch-failure case with the same no-placeholder rule.
**Your decision:** Approved this policy and had it reflected across all four locations.
**What you changed and why:** Requirement #2, Requirement #9, User Story 1 acceptance criteria, and Edge Case #3 were written/updated so no part of the document implies an automatic "Untitled" fallback exists.
**How you verified it:** Not established in the available Copilot history; verified separately during implementation/testing (the implemented `BookmarkService` blocks the save and returns a failure result when title-fetch fails, matching this policy).
**Outcome:** Adopted; the "no placeholder, ask the user" rule is now consistent across Requirement #2, Requirement #9, User Story 1, and Edge Case #3.
**Iteration:** Single pass — the policy was stated once and propagated to all four sections in that pass.
**Approx. time:** Not established in the available Copilot history.
**Learning:** A single policy decision (no silent placeholders) needed to be threaded through multiple sections (requirement, cross-cutting validation, acceptance criteria, edge case) to stay consistent — worth checking for this kind of cross-section consistency whenever a "never do X" rule is introduced.

### Evidence E-PLAN-03
**SDLC activity:** Planning — acceptance-criteria refinement
**Task/feature:** Tagging — tag normalization rules
**Context given to AI:** The in-progress planning document's Tagging requirement and User Story 2 (Organize with tags) draft
**Prompt/request:** User stated that tags should not be case-sensitive.
**AI response summary:** Copilot updated User Story 2's acceptance criteria to explicitly state that tags are trimmed and deduplicated case-insensitively, with an example ("Work", "work", " work " treated as the same tag), matching the normalization rule already described in Requirement #3 (trim, case-insensitive, dedupe).
**Your decision:** Approved the explicit case-insensitive wording in the acceptance criteria.
**What you changed and why:** User Story 2's acceptance criteria were updated to state the case-insensitive dedupe rule explicitly, rather than leaving it only implied by Requirement #3.
**How you verified it:** Not established in the available Copilot history; verified separately during implementation/testing (the implemented `TagNormalizer` lowercases and dedupes tags, matching this acceptance criterion).
**Outcome:** Adopted; User Story 2 now explicitly states the case-insensitive dedupe behavior.
**Iteration:** Single turn.
**Approx. time:** Not established in the available Copilot history.
**Learning:** Stating a normalization rule in the requirement isn't sufficient by itself — restating it explicitly in the acceptance criteria removes ambiguity for whoever implements/tests the story.

### Evidence E-PLAN-04
**SDLC activity:** Planning — new requirement/feature addition
**Task/feature:** Filter by Tag — handling bookmarks with zero tags
**Context given to AI:** The in-progress planning document's Filter by Tag requirement and User Story 3 draft
**Prompt/request:** User requested a default "Untagged" tag/filter option for bookmarks that have no tags.
**AI response summary:** Copilot updated Requirement #5 (Filter by Tag) to specify a built-in virtual "Untagged" tag that always appears in the tag list/dropdown and, when selected, shows only bookmarks with zero tags; User Story 3's acceptance criteria were updated to state the same rule, including that "Untagged" is always available even if no bookmarks currently lack tags.
**Your decision:** Approved adding the "Untagged" option as a virtual (non-stored) filter rather than a real tag.
**What you changed and why:** Requirement #5 and User Story 3 were updated to describe "Untagged" as always-present in the filter UI and as a query condition (zero tags), not a tag value stored on any bookmark.
**How you verified it:** Not established in the available Copilot history; verified separately during implementation/testing (the implemented API exposes an `untagged` query flag rather than storing "Untagged" as a tag, matching this description).
**Outcome:** Adopted; Requirement #5 and User Story 3 both describe the built-in "Untagged" virtual filter consistently.
**Iteration:** Single turn.
**Approx. time:** Not established in the available Copilot history.
**Learning:** Deciding early that "Untagged" is a virtual/computed filter rather than a real stored tag avoided a data-model ambiguity that would otherwise have surfaced later in the Data Model design section.

## Planning Outcome

Summary of what this planning phase settled, going into design:

- **Functional scope**: a single-user, locally-run web app for saving, tagging, listing,
  filtering, searching, editing, and deleting bookmarks, built as vertical slices
  (Foundation/Persistence → Add → Tagging → List → Filter by Tag → Search → Edit → Delete →
  Validation/Error handling → UI wiring), per the Requirement Breakdown.
- **Validation & error-handling decisions**: invalid/empty URLs (including disallowed schemes
  like `javascript:`/`data:`) are rejected before persistence with a clear inline message;
  duplicate URLs are detected via a normalized-URL comparison and surfaced as a friendly
  "This URL is already saved" message rather than a silent no-op or generic error; empty/no-match
  states (no bookmarks yet, no search/filter results) use distinct, friendly messaging instead of
  blank screens or errors; deletes require a confirmation step.
- **Title-fetch behavior**: title is optional; when left blank, the app attempts to fetch the
  page's `<title>` only at save time; if the fetch fails or yields no usable title (timeout,
  unreachable host, non-HTML response, no `<title>` found), the save is blocked and the user is
  explicitly prompted to type a title — an automatic "Untitled" placeholder is never persisted
  (Requirement #2, Requirement #9, User Story 1, Edge Case #3).
- **Tag behavior**: tags are free-text and optional; they are trimmed and deduplicated
  case-insensitively (Requirement #3, User Story 2), and a built-in virtual "Untagged" filter is
  always available in the tag list/dropdown to surface bookmarks with zero tags, without storing
  "Untagged" as a real tag (Requirement #5, User Story 3).
- **Persistence decision (planning level)**: a local JSON file-backed store (`bookmarks.json`),
  loaded into memory at startup and written back via an atomic write-to-temp-then-rename pattern
  on every create/update/delete, satisfying the "no external database/paid cloud service"
  constraint while surviving a restart with no data loss.
- **NFR/security considerations carried into design**: sub-500ms list/search/filter at 1,000
  bookmarks; zero data loss across restarts via atomic writes; app usable within 5 seconds of
  launch; core actions reachable within 2 interactions; WCAG 2.1 AA baseline accessibility;
  output-encoding of all user-supplied/fetched content, `http`/`https`-only URL scheme
  restriction, bounded timeout and capped response size on title-fetch requests, and no
  secrets/credentials required; core domain logic unit-tested behind a swappable persistence
  interface.
- **Main risks/open questions carried into design/build**: SSRF and parsing fragility risk from
  fetching external `<title>` content; possible false positives/negatives in normalized-URL
  duplicate detection (e.g. tracking query params); no authentication if ever exposed beyond
  localhost; JSON file storage's lack of transactional guarantees at scale. Open questions
  carried forward: strict-block vs. warn-and-override for duplicates, single- vs. multi-tag
  filtering, soft- vs. hard-delete, the realistic max bookmark count beyond the 1,000-record
  benchmark, and whether import/export is in scope.
