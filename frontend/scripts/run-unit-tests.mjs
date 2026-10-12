/**
 * Run the `*.test.ts` files under lib/ and components/.
 *
 * They are plain node:test files over pure functions — no React, no DOM — but they import
 * through the `@/…` alias, which Node cannot resolve on its own. So they are compiled to a
 * throwaway directory first, with a `node_modules/@` symlink standing in for the alias, and run
 * from there. That is the whole trick; there is no test framework to learn.
 *
 * Type errors do not stop the run: `npm run build` and `tsc --noEmit` are what type-check this
 * project, and a missing @types/node here would otherwise block tests that work perfectly.
 */
import { execFileSync } from "node:child_process";
import { mkdtempSync, mkdirSync, symlinkSync, writeFileSync, readdirSync, statSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";

const root = resolve(import.meta.dirname, "..");

function testFiles(dir) {
  const found = [];
  for (const entry of readdirSync(dir)) {
    if (entry === "node_modules" || entry.startsWith(".")) continue;
    const path = join(dir, entry);
    if (statSync(path).isDirectory()) found.push(...testFiles(path));
    else if (entry.endsWith(".test.ts")) found.push(path);
  }
  return found;
}

const files = [join(root, "lib"), join(root, "components")].flatMap(testFiles);
if (files.length === 0) {
  console.log("No unit tests found.");
  process.exit(0);
}

const out = mkdtempSync(join(tmpdir(), "simbaflow-tests-"));
const config = join(out, "tsconfig.json");
writeFileSync(
  config,
  JSON.stringify({
    compilerOptions: {
      outDir: join(out, "build"),
      module: "commonjs",
      target: "es2022",
      jsx: "react-jsx",
      esModuleInterop: true,
      skipLibCheck: true,
      baseUrl: root,
      paths: { "@/*": ["./*"] },
    },
    files,
  }),
);

try {
  execFileSync("npx", ["tsc", "-p", config], { stdio: "pipe" });
} catch {
  // Emitted anyway — see the note above.
}

const build = join(out, "build");
mkdirSync(join(build, "node_modules"), { recursive: true });
try {
  symlinkSync(build, join(build, "node_modules", "@"));
} catch {
  // Already there on a re-run inside the same directory.
}

function builtTests(dir) {
  const found = [];
  for (const entry of readdirSync(dir)) {
    if (entry === "node_modules") continue;
    const path = join(dir, entry);
    if (statSync(path).isDirectory()) found.push(...builtTests(path));
    else if (entry.endsWith(".test.js")) found.push(path);
  }
  return found;
}
execFileSync("node", ["--test", ...builtTests(build)], {
  stdio: "inherit",
  env: { ...process.env, NODE_PATH: join(root, "node_modules") },
});
