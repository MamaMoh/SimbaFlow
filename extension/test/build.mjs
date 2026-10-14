/**
 * Builds a runnable copy of the fixture with the real page functions inlined.
 *
 * The fixture links page-functions.js the way the extension would, but a page opened from the
 * file system in a preview pane does not fetch sibling scripts. Inlining the actual file — never
 * a copy of it — keeps the thing under test the thing that ships.
 */
import { readFileSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";

const here = dirname(new URL(import.meta.url).pathname);
const source = readFileSync(join(here, "..", "page-functions.js"), "utf8");
const fixture = readFileSync(join(here, "insurance-form.html"), "utf8");

const out = fixture.replace(
  '<script src="../page-functions.js"></script>',
  `<script>\n${source}\nwindow.fillOnPage = fillOnPage;\nwindow.collectLabelsOnPage = collectLabelsOnPage;\n</script>`,
);

writeFileSync(join(here, "harness.generated.html"), out);
console.log("harness.generated.html written");
