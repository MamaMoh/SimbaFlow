# SimbaFlow Form Filler

Fills a candidate's details into the insurance and visa portals and the LMIS Create Contract
form, from the record already held in SimbaFlow.

## What it knows, and what it does not

Almost nothing. The extension can ask SimbaFlow what a form wants, find a box on a page by the
label printed beside it, and type into one. It does not know what a sponsor is, which portal it
is standing on, or that a date is a date.

All of that lives in `frontend/lib/site-bridge/` and arrives over the wire:

- `GET /api/site-maps` — the forms we know, and what each box is called
- `GET /api/site-maps/plan?site=…&candidateId=…` — one candidate, as a list of labels and text

**So a portal that renames a box is fixed by a deploy of SimbaFlow**, not by twenty people
re-installing this. That is the entire reason it is built this way.

## Filling by paste

The second way in. On the Candidates or LMIS board, right-click a candidate and choose **Copy to
insurance**. SimbaFlow puts that candidate on the clipboard as a finished fill — the same plan
the toolbar would have fetched — and pasting it into the insurance form fills every box at once.
A batch of twenty is right-click, copy, switch tab, paste, check, Add Member, twenty times.
**Copy to LMIS contract** does the same for the Create Contract dialog on a labourer's LMIS
record: the contract dates, passport, visa and employer. Employer city is LMIS's own dropdown
widget, so the card names the city to pick instead of picking it.

- The payload is one line, `simbaflow-fill:1:` and base64 JSON, written by
  `frontend/lib/site-bridge/clipboard.ts` and read by `paste.js`. That file's test pins the exact
  bytes, so changing the format on one side fails until the other side changes too.
- A copy is good for twenty minutes. Copy Hiwot at nine and paste at four, and the form meant
  for somebody else would get Hiwot; an old copy is refused instead.
- It fills only a page that prints at least three of the form's own labels — the same test the
  popup uses — so pasting into a webmail box or the visa form fills nothing. A payload is never
  let through into the box either way: it is a candidate's details, encoded but not hidden.
- Any other paste passes through untouched.

The listener has to be on the page before the paste, which the toolbar's one-click access cannot
give. So the popup, on a form it recognises, offers **Fill when I paste here**; turning it on asks
the browser for that one site (`optional_host_permissions`), and `background.js` registers
`paste.js` for exactly the sites granted. Taking the access away — from the switch or from
`chrome://extensions` — unregisters it. There is still no list of portals in this folder.

## Permissions

- `host_permissions` is **only** `https://app.laba.et`. Its requests carry the signed-in session
  cookie, so there is no second credential to issue, store on a laptop, or forget to revoke.
- No portal is listed anywhere. `activeTab` grants access to the one tab you are looking at, at
  the moment you click the toolbar button, and nothing else.
- `optional_host_permissions` covers any site, and is never granted wholesale: the popup asks
  for one origin at a time, when a person turns on filling by paste for the form in front of them.
- The page is recognised by the labels printed on it, not by its hostname. A portal that moves
  to a new address keeps working.

## What it will not do

- **It never submits.** It fills boxes; a person reads them and presses the form's own button.
- **It never ticks a checkbox or a radio.** "I hereby declare that the information provided is
  true" is a person's attestation on a government application.
- **It will not type into a dropdown that is not a real `<select>`.** Setting the text of
  somebody's custom widget leaves a box that reads correctly and has selected nothing, which is
  worse than an empty box because it looks finished. Those are reported by name instead.

That last one is the known limitation and the next thing to work on. It needs handling per
portal, and it is the fragile sort of work that breaks when a portal changes — which is why it
is not in the first version.

## Running it while developing

1. `chrome://extensions` → **Developer mode** → **Load unpacked** → this folder.
2. Sign in to SimbaFlow in the same browser.
3. Open a portal form, click the toolbar button, search for a candidate, press **Fill**.

To point it at a local SimbaFlow, open the popup, inspect it, and run:

```js
chrome.storage.local.set({ origin: "http://localhost:3000" })
```

`host_permissions` in `manifest.json` has to allow that origin too.

## Testing the part that touches a page

`page-functions.js` holds the two functions that run inside the portal's page. They are injected,
not called, so they can reference nothing outside themselves — keeping them in their own file is
what makes that rule visible rather than a trap.

```bash
node test/build.mjs
```

builds `test/harness.generated.html`: the fixture in `test/insurance-form.html` with the real
`page-functions.js` inlined, so what is tested is what ships. Open it in a browser and run
`window.__simbaflow.fill([...])` from the console.

## Testing paste, end to end

```bash
cd frontend && npm ci          # once, for Playwright
node ../extension/test/paste-e2e.mjs [--shots <dir>]
```

loads this folder unpacked into Chromium, serves the insurance fixture locally, puts payloads on
the real clipboard and presses Ctrl+V: a fill, an ordinary paste, a stale copy, the wrong page, a
newer format, and a withdrawn site. `CHROMIUM_PATH` picks a browser other than Playwright's own.

## Looking at the popup

```bash
node test/build-popup-preview.mjs
```

builds `test/popup-preview.generated.html`: the real popup.html, popup.css and popup.js with the
extension APIs and SimbaFlow stubbed, so the interface can be reviewed in a normal tab without
installing anything. What is reviewed is the actual rendering code rather than a mock-up of it,
which is how two layout bugs were found — a list missing the class its own stylesheet targeted,
and a row that never became a row.

The popup uses SimbaFlow's own design tokens, copied from `frontend/app/globals.css`, and the
app's typeface, bundled in `fonts/` because an extension cannot fetch one. Geist is under the SIL
Open Font License; the licence ships beside it.

## Publishing

Unlisted on the Chrome Web Store, linked from inside SimbaFlow. Unlisted means it has a URL but
does not appear in search, which is right for a tool your agencies use and the public does not.

Ready: the icons are in `icons/` (regenerate them with `python3 icons/make-icons.py`), and the
privacy policy is served publicly at `https://app.laba.et/privacy`, from
`frontend/app/privacy/page.tsx`. It describes what this extension actually does — if the
extension changes, that page changes with it.

Still to do:

- A one-time $5 Chrome developer account.
- Upload `frontend/public/simbaflow-form-filler.zip`, which `npm run build` regenerates from
  this folder.
- Set `NEXT_PUBLIC_EXTENSION_STORE_URL` to the listing. The settings screen then offers
  "Add to Chrome" instead of the download and the three manual steps.

Until then SimbaFlow hands out the zip itself, from Settings → Browser helper.
