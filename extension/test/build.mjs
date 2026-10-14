/**
 * Builds a runnable copy of the fixture with the real page functions inlined.
 *
 * The fixture links page-functions.js the way the extension would, but a page opened from the
 * file system in a preview pane does not fetch sibling scripts. Inlining the actual file — never
 * a copy of it — keeps the thing under test the thing that ships.
 */
import { readFileSync, readdirSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";

const here = dirname(new URL(import.meta.url).pathname);
const source = readFileSync(join(here, "..", "page-functions.js"), "utf8");

// Every fixture in here, not one: each is a page shape that broke something once, and a shape
// that broke something once is the shape to keep testing.
for (const name of readdirSync(here)) {
  if (!name.endsWith(".html") || name.endsWith(".generated.html")) continue;

  const out = readFileSync(join(here, name), "utf8").replace(
    '<script src="../page-functions.js"></script>',
    `<script>\n${source}\n</script>`,
  );
  const built = name.replace(/\.html$/, ".generated.html");
  writeFileSync(join(here, built), out);
  console.log(`${built} written`);
}
