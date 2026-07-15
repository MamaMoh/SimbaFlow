/**
 * Exports data to CSV and triggers a browser download.
 * @param data Array of objects to export
 * @param filename Filename without extension
 * @param columns Optional column mapping { header: key }
 */
export function exportToCsv<T extends Record<string, any>>(
  data: T[],
  filename: string,
  columns?: Array<{ header: string; key: string }>
) {
  if (data.length === 0) return;

  const cols = columns ?? Object.keys(data[0]).map((key) => ({ header: key, key }));

  const header = cols.map((c) => `"${c.header}"`).join(",");
  const rows = data.map((row) =>
    cols.map((c) => {
      const val = row[c.key];
      if (val == null) return '""';
      const str = String(val).replace(/"/g, '""');
      return `"${str}"`;
    }).join(",")
  );

  const csv = [header, ...rows].join("\n");
  const blob = new Blob([csv], { type: "text/csv;charset=utf-8;" });
  const url = URL.createObjectURL(blob);

  const link = document.createElement("a");
  link.href = url;
  link.download = `${filename}-${new Date().toISOString().slice(0, 10)}.csv`;
  link.click();
  URL.revokeObjectURL(url);
}
