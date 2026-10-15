const form = document.getElementById('add-form');
const urlInput = document.getElementById('url');
const titleInput = document.getElementById('title');
const tagsInput = document.getElementById('tags');
const saveButton = document.getElementById('save-button');
const cancelEditButton = document.getElementById('cancel-edit-button');
const message = document.getElementById('form-message');

const editBookmarkSelect = document.getElementById('edit-bookmark-select');
const editForm = document.getElementById('edit-form');
const editUrl = document.getElementById('edit-url');
const editTitle = document.getElementById('edit-title');
const editTags = document.getElementById('edit-tags');
const saveEditButton = document.getElementById('save-edit-button');
const editMessage = document.getElementById('edit-message');

const deleteBookmarkSelect = document.getElementById('delete-bookmark-select');
const openDeleteButton = document.getElementById('open-delete-button');
const deleteMessage = document.getElementById('delete-message');

const listMessage = document.getElementById('list-message');
const bookmarkList = document.getElementById('bookmark-list');
const bookmarkCount = document.getElementById('bookmark-count');
const tagFilter = document.getElementById('tag-filter');
const searchBox = document.getElementById('search-box');
const deleteDialog = document.getElementById('delete-dialog');
const deleteDialogMessage = document.getElementById('delete-dialog-message');
const deleteConfirmButton = document.getElementById('delete-confirm-button');
const deleteCancelButton = document.getElementById('delete-cancel-button');

let pendingDeleteId = null;
let searchDebounceTimer = null;
let allBookmarks = [];

// ---------------- Tabs ----------------
const tabButtons = [...document.querySelectorAll('.tab-button')];
const tabPanels = [...document.querySelectorAll('.tab-panel')];

function activateTab(tabName) {
  for (const button of tabButtons) {
    const active = button.dataset.tab === tabName;
    button.classList.toggle('active', active);
    button.setAttribute('aria-selected', String(active));
  }

  for (const panel of tabPanels) {
    const active = panel.dataset.panel === tabName;
    panel.classList.toggle('active', active);
    panel.hidden = !active;
  }

  if (tabName === 'edit') {
    populateActionSelects();
    updateEditForm();
  } else if (tabName === 'delete') {
    populateActionSelects();
  } else if (tabName === 'search') {
    loadTagFilterOptions().then(loadBookmarks);
  }
}

tabButtons.forEach((button) => {
  button.addEventListener('click', () => activateTab(button.dataset.tab));
});

// ---------------- Add ----------------
form.addEventListener('submit', async (event) => {
  event.preventDefault();

  const url = urlInput.value.trim();
  const title = titleInput.value.trim();
  const tags = parseTags(tagsInput.value);

  saveButton.disabled = true;
  setMessage('Saving…', 'info');

  try {
    const response = await fetch('/api/bookmarks/', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ url, title: title || null, tags })
    });

    const body = await response.json();

    if (!response.ok) {
      setMessage(body.error ?? 'Something went wrong.', 'error');
      return;
    }

    setMessage(`Saved "${body.title}".`, 'success');
    form.reset();
    await refreshBookmarks();
  } catch {
    setMessage('Could not reach the server. Please try again.', 'error');
  } finally {
    saveButton.disabled = false;
  }
});

cancelEditButton.addEventListener('click', () => {
  form.reset();
  setMessage('', '');
  cancelEditButton.hidden = true;
});

// ---------------- Edit ----------------
editBookmarkSelect.addEventListener('change', updateEditForm);

editForm.addEventListener('submit', async (event) => {
  event.preventDefault();

  const bookmark = getSelectedBookmark(editBookmarkSelect);
  if (!bookmark) {
    setStatus(editMessage, 'Please select a bookmark first.', 'error');
    return;
  }

  saveEditButton.disabled = true;
  setStatus(editMessage, 'Saving changes…', 'info');

  try {
    const response = await updateBookmark(bookmark, {
      url: editUrl.value.trim(),
      title: editTitle.value.trim(),
      tags: parseTags(editTags.value)
    });

    if (!response.ok) {
      setStatus(editMessage, response.body?.error ?? 'Could not save changes.', 'error');
      return;
    }

    setStatus(editMessage, 'Changes saved successfully.', 'success');
    await refreshBookmarks();
    updateEditForm();
  } catch {
    setStatus(editMessage, 'Could not reach the server. Please try again.', 'error');
  } finally {
    saveEditButton.disabled = false;
  }
});

// ---------------- Delete ----------------
deleteBookmarkSelect.addEventListener('change', () => setStatus(deleteMessage, '', ''));

openDeleteButton.addEventListener('click', () => {
  const bookmark = getSelectedBookmark(deleteBookmarkSelect);
  if (!bookmark) {
    setStatus(deleteMessage, 'Please select a bookmark first.', 'error');
    return;
  }

  pendingDeleteId = bookmark.id;
  deleteDialogMessage.textContent = `Delete "${bookmark.title}"? This cannot be undone.`;
  deleteDialog.showModal();
});

deleteCancelButton.addEventListener('click', () => {
  pendingDeleteId = null;
  deleteDialog.close();
});

deleteConfirmButton.addEventListener('click', async () => {
  if (pendingDeleteId === null) {
    return;
  }

  const id = pendingDeleteId;
  deleteConfirmButton.disabled = true;

  try {
    const response = await fetch(`/api/bookmarks/${id}`, { method: 'DELETE' });
    if (!response.ok) {
      setStatus(deleteMessage, 'Could not delete that bookmark. Please try again.', 'error');
      return;
    }

    setStatus(deleteMessage, 'Bookmark deleted successfully.', 'success');
    await refreshBookmarks();
  } catch {
    setStatus(deleteMessage, 'Could not reach the server. Please try again.', 'error');
  } finally {
    deleteConfirmButton.disabled = false;
    pendingDeleteId = null;
    deleteDialog.close();
  }
});

// ---------------- Search & Filter ----------------
tagFilter.addEventListener('change', () => loadBookmarks());

searchBox.addEventListener('input', () => {
  clearTimeout(searchDebounceTimer);
  searchDebounceTimer = setTimeout(() => loadBookmarks(), 250);
});

async function loadTagFilterOptions() {
  const selectedValue = tagFilter.value;

  try {
    const response = await fetch('/api/tags');
    if (!response.ok) {
      return;
    }

    const tags = await response.json();
    const options = [new Option('All tags', ''), new Option('Untagged', '__untagged__')];
    for (const tag of tags) {
      options.push(new Option(tag, tag));
    }

    tagFilter.replaceChildren(...options);
    tagFilter.value = [...tagFilter.options].some((option) => option.value === selectedValue)
      ? selectedValue
      : '';
  } catch {
    // Filter dropdown is a convenience; leave existing options in place if this fails.
  }
}

async function loadBookmarks() {
  const selectedTag = tagFilter.value;
  const searchTerm = searchBox.value.trim();
  const params = new URLSearchParams();

  if (selectedTag === '__untagged__') {
    params.set('untagged', 'true');
  } else if (selectedTag) {
    params.set('tag', selectedTag);
  }
  if (searchTerm) {
    params.set('search', searchTerm);
  }

  try {
    const response = await fetch(`/api/bookmarks/?${params.toString()}`);
    if (!response.ok) {
      setListMessage('Could not load bookmarks. Please refresh the page.');
      return;
    }

    const bookmarks = await response.json();
    renderBookmarks(bookmarks, selectedTag !== '' || searchTerm !== '');
  } catch {
    setListMessage('Could not reach the server. Please refresh the page.');
  }
}

function renderBookmarks(bookmarks, isFiltered) {
  bookmarkList.replaceChildren();
  bookmarkCount.textContent = `${bookmarks.length} ${bookmarks.length === 1 ? 'bookmark' : 'bookmarks'}`;

  if (bookmarks.length === 0) {
    setListMessage(isFiltered
      ? 'No bookmarks match your search or filter.'
      : 'No bookmarks yet — add your first one.');
    return;
  }

  setListMessage('');

  for (const bookmark of bookmarks) {
    const item = document.createElement('li');
    item.className = 'bookmark-card';

    const details = document.createElement('div');
    details.className = 'bookmark-details';

    const title = document.createElement('a');
    title.href = bookmark.url;
    title.target = '_blank';
    title.rel = 'noopener noreferrer';
    title.textContent = bookmark.title;
    details.appendChild(title);

    const url = document.createElement('span');
    url.className = 'bookmark-url';
    url.textContent = bookmark.url;
    details.appendChild(url);

    if (bookmark.tags.length > 0) {
      const tags = document.createElement('div');
      tags.className = 'tag-list';
      for (const tag of bookmark.tags) {
        const tagBadge = document.createElement('span');
        tagBadge.className = 'tag-badge';
        tagBadge.textContent = tag;
        tags.appendChild(tagBadge);
      }
      details.appendChild(tags);
    } else {
      const untagged = document.createElement('span');
      untagged.className = 'untagged-label';
      untagged.textContent = 'Untagged';
      details.appendChild(untagged);
    }

    const meta = document.createElement('span');
    meta.className = 'bookmark-date';
    meta.textContent = formatDate(bookmark.createdAt);
    details.appendChild(meta);

    item.appendChild(details);
    bookmarkList.appendChild(item);
  }
}

// ---------------- Shared helpers ----------------
async function refreshBookmarks() {
  await loadAllBookmarks();
  await loadTagFilterOptions();
  await loadBookmarks();
  populateActionSelects();
}

async function loadAllBookmarks() {
  try {
    const response = await fetch('/api/bookmarks/');
    if (!response.ok) {
      return;
    }
    allBookmarks = await response.json();
  } catch {
    allBookmarks = [];
  }
}

function populateActionSelects() {
  const selects = [editBookmarkSelect, deleteBookmarkSelect];
  const currentValues = selects.map((select) => select.value);

  selects.forEach((select, index) => {
    const placeholder = new Option('Select a bookmark…', '');
    const options = [placeholder];
    for (const bookmark of allBookmarks) {
      options.push(new Option(bookmark.title, bookmark.id));
    }
    select.replaceChildren(...options);
    if (allBookmarks.some((bookmark) => bookmark.id === currentValues[index])) {
      select.value = currentValues[index];
    }
  });
}

function getSelectedBookmark(select) {
  return allBookmarks.find((bookmark) => bookmark.id === select.value) ?? null;
}

function updateEditForm() {
  const bookmark = getSelectedBookmark(editBookmarkSelect);
  if (!bookmark) {
    editUrl.value = '';
    editTitle.value = '';
    editTags.value = '';
    return;
  }

  editUrl.value = bookmark.url;
  editTitle.value = bookmark.title;
  editTags.value = bookmark.tags.join(', ');
}

async function updateBookmark(bookmark, changes) {
  const response = await fetch(`/api/bookmarks/${bookmark.id}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      url: changes.url ?? bookmark.url,
      title: changes.title ?? bookmark.title,
      tags: changes.tags ?? bookmark.tags
    })
  });

  let body = null;
  try {
    body = await response.json();
  } catch {
    // DELETE/empty response is not expected here, but keep the helper defensive.
  }
  return { ok: response.ok, body };
}

function parseTags(value) {
  return value
    .split(',')
    .map((tag) => tag.trim())
    .filter((tag) => tag.length > 0);
}

function formatDate(value) {
  if (!value) return '';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '';
  return `Added ${date.toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' })}`;
}

function setListMessage(text) {
  listMessage.textContent = text;
}

function setMessage(text, kind) {
  setStatus(message, text, kind);
}

function setStatus(element, text, kind) {
  element.textContent = text;
  element.className = `status-message ${kind}`.trim();
}

loadAllBookmarks().then(() => {
  populateActionSelects();
  return loadTagFilterOptions();
}).then(loadBookmarks);
