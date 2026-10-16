"use client";

import { useEffect, useMemo, useState } from "react";
import { toQueryParam, type ColumnFilter } from "@/lib/data-table/column-filters";

/**
 * The two things every board asks the API for: a search string and a set of column filters.
 *
 * It was written out longhand on the candidates board first and is the same eleven lines on
 * every other one — a piece of state, a timer, a memo and the care about an empty box. Copied
 * seventeen times it would be seventeen chances to get the debounce right and one place where
 * somebody did not.
 *
 * `globalFilter` is the toolbar's search box, updated on every keystroke and handed to the
 * table so the box shows what was typed. `search` follows a beat behind and is what travels, so
 * typing a name is one request rather than one per letter.
 */
export function useBoardQuery(delay = 300) {
  const [globalFilter, setGlobalFilter] = useState("");
  const [search, setSearch] = useState("");
  const [filters, setFilters] = useState<ColumnFilter[]>([]);

  // A board opened from My work arrives as `?q=<passport>` and starts narrowed to that one
  // candidate, so the row clicked there is the row in front of you here. Read once after mount:
  // the server render has no address bar to read, and a box filled before hydration would not
  // match the one it rendered.
  useEffect(() => {
    const q = new URLSearchParams(window.location.search).get("q")?.trim();
    if (q) {
      setGlobalFilter(q);
      setSearch(q);
    }
  }, []);

  useEffect(() => {
    // `?? ""` is load-bearing. An empty search box means "no filter" to TanStack, which it
    // signals as undefined — and trimming undefined throws before it can clear `search`, so
    // emptying the box used to leave the list narrowed by whatever had been typed last.
    const timer = setTimeout(() => setSearch((globalFilter ?? "").trim()), delay);
    return () => clearTimeout(timer);
  }, [globalFilter, delay]);

  // Only the finished ones travel. A "greater than" with an empty box is the ordinary state
  // while somebody is still typing, and it should leave the list alone rather than empty it.
  const filterParam = useMemo(() => toQueryParam(filters), [filters]);

  return { globalFilter, setGlobalFilter, search, filters, setFilters, filterParam };
}

/**
 * The query string a board appends for its search and its funnels.
 *
 * Both are optional and both are encoded, which is the part worth not retyping: a passport
 * number with a space in it, or a filter whose value contains an ampersand, otherwise ends the
 * query string early and the board quietly returns everything.
 */
export function boardQueryString(search: string, filterParam: string): string {
  return (
    (search ? `&search=${encodeURIComponent(search)}` : "") +
    (filterParam ? `&filters=${encodeURIComponent(filterParam)}` : "")
  );
}
