/**
 * Packs the browser extension into public/, so SimbaFlow can hand it to people.
 *
 * It lives in public/ rather than being zipped on request because of where the web image is
 * built from: the build context is repo/frontend, so extension/ is not there at build time and
 * nothing inside the container could produce it. The zip is therefore committed, and this script
 * is what keeps it honest — it runs before every local build, and a stale download is one
 * forgotten `npm run build` away rather than something nobody ever notices.
 *
 * Inside Docker there is no extension/ to read and this quietly does nothing, leaving the
 * committed file to be served.
 */
import { execFileSync } from "node:child_process";
import { existsSync, mkdirSync, rmSync, statSync } from "node:fs";
import { dirname, join, resolve } from "node:path";

const root = resolve(import.meta.dirname, "..");
const source = resolve(root, "..", "extension");
const out = join(root, "public", "simbaflow-form-filler.zip");

if (!existsSync(source)) {
  console.log("extension/ is not here — keeping the committed zip");
  process.exit(0);
}

mkdirSync(dirname(out), { recursive: true });
rmSync(out, { force: true });

// Not the test fixture, not the generated harness, not the icon script: what a person installs
// should be what runs, and nothing else to wonder about.
execFileSync(
  "zip",
  ["-r", "-q", "-X", out, ".", "-x", "test/*", ".gitignore", "icons/make-icons.py"],
  { cwd: source },
);

console.log(`simbaflow-form-filler.zip  ${statSync(out).size} bytes`);
