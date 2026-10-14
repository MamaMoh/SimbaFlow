/**
 * Tells SimbaFlow that the helper is installed.
 *
 * The settings screen offers people a download, and ought to stop offering it once they have it.
 * Asking the extension directly would mean the page knowing its id, which is not stable for an
 * extension loaded from a folder — so instead the extension leaves a mark on the page and the
 * page looks for it.
 *
 * At document_start, so the mark is already there when the app mounts and the screen does not
 * have to flicker through "not installed" first.
 *
 * This runs on SimbaFlow's own pages and nowhere else, and does one thing: sets an attribute
 * saying which version is installed. It reads nothing.
 */
document.documentElement.dataset.simbaflowHelper = chrome.runtime.getManifest().version;
