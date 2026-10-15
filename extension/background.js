/**
 * Keeps filling-on-paste switched on for exactly the sites a person has switched it on for.
 *
 * The extension asks for no portal up front, and that stays true. Filling on paste needs to be
 * listening before the paste arrives, which the one-tab, one-click access of the toolbar button
 * cannot give — so the popup offers a switch on a form it recognises, and the browser asks the
 * person whether to allow this one site. That grant is the whole of the list: this file holds
 * no hostnames of its own.
 *
 * Everything here follows from the grants. Whenever they change, from the popup or from the
 * browser's own extension settings, the registered scripts are made to match — so taking access
 * away anywhere takes the listening away with it, and there is no second list to drift.
 */

const SCRIPT_PREFIX = "paste:";

/** SimbaFlow itself is where payloads are made, never where they are pasted. */
const HOME = ["https://app.laba.et"];

async function grantedSites() {
  const { origins = [] } = await chrome.permissions.getAll();
  return origins.filter(
    (pattern) => /^https?:\/\/[^*/]+\/\*$/.test(pattern) && !HOME.some((h) => pattern.startsWith(`${h}/`)),
  );
}

async function sync() {
  const wanted = new Set(await grantedSites());
  const registered = (await chrome.scripting.getRegisteredContentScripts()).filter((s) =>
    s.id.startsWith(SCRIPT_PREFIX),
  );
  const have = new Set(registered.map((s) => s.id.slice(SCRIPT_PREFIX.length)));

  const stale = registered.filter((s) => !wanted.has(s.id.slice(SCRIPT_PREFIX.length)));
  if (stale.length) {
    await chrome.scripting.unregisterContentScripts({ ids: stale.map((s) => s.id) });
  }

  const fresh = [...wanted].filter((pattern) => !have.has(pattern));
  if (fresh.length) {
    await chrome.scripting.registerContentScripts(
      fresh.map((pattern) => ({
        id: SCRIPT_PREFIX + pattern,
        matches: [pattern],
        js: ["page-functions.js", "paste.js"],
        // These portals put their forms in frames as often as not. The paste happens in the
        // frame that has the cursor, so that is where the listener has to be.
        allFrames: true,
        runAt: "document_idle",
        persistAcrossSessions: true,
      })),
    );
  }
}

/** One sync at a time: two overlapping ones would both try to register the same new site. */
let running = Promise.resolve();
const resync = () => {
  running = running.then(sync).catch((err) => console.error("SimbaFlow: paste sites", err));
  return running;
};

chrome.runtime.onInstalled.addListener(resync);
chrome.runtime.onStartup.addListener(resync);
chrome.permissions.onAdded.addListener(resync);
chrome.permissions.onRemoved.addListener(resync);

// The popup waits on this before it injects into the tab in front of it, so the page it is
// looking at starts listening now rather than on the next load.
chrome.runtime.onMessage.addListener((message, _sender, reply) => {
  if (message?.type !== "paste-sites:sync") return false;
  resync().then(() => reply({ ok: true }));
  return true;
});
