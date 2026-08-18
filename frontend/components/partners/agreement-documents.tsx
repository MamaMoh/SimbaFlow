"use client";

import { useRef, useState } from "react";
import useSWR from "swr";
import { toast } from "sonner";
import { FileText, Trash2, Upload } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { LoadError } from "@/components/ui/page-alert";

type AgreementDocument = {
  id: string;
  title: string | null;
  originalFileName: string;
  contentType: string;
  fileSizeBytes: number;
  uploadedAt: string;
  uploadedBy: string | null;
};

const fetcher = (url: string) => fetch(url).then((r) => r.json());

function humanSize(bytes: number) {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} KB`;
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
}

/**
 * Signed contracts attached to one partner agreement.
 *
 * `linkId` is the agreement, not the partner: an agency can only ever see documents on its own
 * agreement, which is what the API enforces.
 */
export function AgreementDocuments({
  linkId,
  canEdit,
}: {
  linkId?: string;
  canEdit: boolean;
}) {
  const [busy, setBusy] = useState(false);
  const [title, setTitle] = useState("");
  const fileRef = useRef<HTMLInputElement>(null);

  const key = linkId ? `/api/proxy/partners/links/${linkId}/documents` : null;
  const { data, error, mutate } = useSWR(key, fetcher, { revalidateOnFocus: false });
  const docs: AgreementDocument[] = data?.data || [];

  if (!linkId) {
    return (
      <p className="rounded-md border border-dashed p-4 text-sm text-muted-foreground">
        Link this partner to your agency before attaching contract documents.
      </p>
    );
  }

  const onUpload = async (file: File) => {
    setBusy(true);
    try {
      const body = new FormData();
      body.append("file", file);
      if (title.trim()) body.append("title", title.trim());
      const res = await fetch(`/api/proxy/partners/links/${linkId}/documents`, {
        method: "POST",
        body,
      });
      const json = await res.json().catch(() => null);
      if (res.ok && json?.isSuccess) {
        toast.success("Contract uploaded");
        setTitle("");
        if (fileRef.current) fileRef.current.value = "";
        mutate();
      } else {
        toast.error(json?.error || "Upload failed");
      }
    } catch {
      toast.error("Upload failed");
    } finally {
      setBusy(false);
    }
  };

  const onDelete = async (docId: string) => {
    setBusy(true);
    try {
      const res = await fetch(`/api/proxy/partners/links/${linkId}/documents/${docId}`, {
        method: "DELETE",
      });
      const json = await res.json().catch(() => null);
      if (res.ok && json?.isSuccess) {
        toast.success("Document removed");
        mutate();
      } else {
        toast.error(json?.error || "Could not remove the document");
      }
    } finally {
      setBusy(false);
    }
  };

  if (error) return <LoadError message={error.message} onRetry={() => mutate()} />;

  return (
    <div className="space-y-4">
      {canEdit ? (
        <div className="rounded-xl border bg-card p-4 shadow-sm">
          <div className="grid gap-3 sm:grid-cols-[1fr_auto] sm:items-end">
            <div className="space-y-1.5">
              <Label>Document name (optional)</Label>
              <Input
                value={title}
                onChange={(e) => setTitle(e.target.value)}
                placeholder="e.g. Signed contract 2026"
              />
            </div>
            <div className="space-y-1.5">
              <Label className="sr-only">File</Label>
              <input
                ref={fileRef}
                type="file"
                className="hidden"
                onChange={(e) => {
                  const f = e.target.files?.[0];
                  if (f) onUpload(f);
                }}
              />
              <Button
                type="button"
                disabled={busy}
                onClick={() => fileRef.current?.click()}
                className="bg-green-800 hover:bg-green-900"
              >
                <Upload className="mr-2 h-4 w-4" />
                {busy ? "Uploading…" : "Upload contract"}
              </Button>
            </div>
          </div>
        </div>
      ) : null}

      {docs.length === 0 ? (
        <div className="flex flex-col items-center gap-2 rounded-xl border border-dashed p-8 text-center">
          <FileText className="h-8 w-8 text-muted-foreground" />
          <p className="text-sm text-muted-foreground">
            No contract documents yet{canEdit ? " — upload the signed agreement above." : "."}
          </p>
        </div>
      ) : (
        <ul className="divide-y rounded-xl border bg-card shadow-sm">
          {docs.map((d) => (
            <li key={d.id} className="flex items-center gap-3 p-3">
              <FileText className="h-5 w-5 shrink-0 text-muted-foreground" />
              <div className="min-w-0 flex-1">
                <a
                  href={`/api/proxy/partners/links/${linkId}/documents/${d.id}`}
                  className="block max-w-[380px] truncate text-sm font-medium underline-offset-2 hover:underline"
                  title={d.title || d.originalFileName}
                >
                  {d.title || d.originalFileName}
                </a>
                <p className="text-xs text-muted-foreground">
                  {humanSize(d.fileSizeBytes)} · {new Date(d.uploadedAt).toLocaleDateString()}
                  {d.uploadedBy ? ` · ${d.uploadedBy}` : ""}
                </p>
              </div>
              {canEdit ? (
                <Button
                  type="button"
                  variant="ghost"
                  size="icon"
                  disabled={busy}
                  onClick={() => onDelete(d.id)}
                  aria-label="Remove document"
                >
                  <Trash2 className="h-4 w-4 text-destructive" />
                </Button>
              ) : null}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
