"use client";

import type { CandidateDocument } from "@/types/candidate";
import { Badge } from "@/components/ui/badge";

const TYPE_LABELS: Record<number, string> = {
  0: "Passport",
  1: "Photo",
  2: "Contract",
  3: "CV",
  4: "LMIS",
  5: "Medical",
  6: "Tasheer",
  7: "Ticket",
  8: "Full size photo",
  9: "Visa form",
  99: "Other",
};

type DocumentListProps = {
  documents: CandidateDocument[];
};

export function DocumentList({ documents }: DocumentListProps) {
  if (documents.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center gap-2 py-12 text-center">
        <p className="text-sm font-medium text-foreground/80">No documents yet</p>
        <p className="max-w-xs text-xs text-muted-foreground">
          Upload a passport scan, photo, or contract — or generate a CV / visa form from this page.
        </p>
      </div>
    );
  }

  const grouped = documents.reduce<Record<string, CandidateDocument[]>>((acc, doc) => {
    const label = TYPE_LABELS[doc.documentType] ?? "Other";
    (acc[label] ??= []).push(doc);
    return acc;
  }, {});

  return (
    <div className="space-y-6">
      {Object.entries(grouped).map(([type, docs]) => (
        <div key={type} className="space-y-2">
          <h3 className="text-sm font-medium">{type}</h3>
          <ul className="divide-y rounded-md border">
            {docs.map((doc) => (
              <li
                key={doc.id}
                className="flex items-center justify-between gap-3 px-3 py-2 text-sm"
              >
                <div className="min-w-0">
                  <p className="truncate font-medium">{doc.originalFileName}</p>
                  <p className="text-xs text-muted-foreground">
                    {(doc.fileSizeBytes / 1024).toFixed(1)} KB ·{" "}
                    {new Date(doc.uploadedAt).toLocaleString()}
                    {doc.uploadedBy ? ` · ${doc.uploadedBy}` : ""}
                  </p>
                </div>
                <Badge variant="outline">{doc.contentType}</Badge>
              </li>
            ))}
          </ul>
        </div>
      ))}
    </div>
  );
}
