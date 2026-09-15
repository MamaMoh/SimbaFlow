"use client";

import { useRef, useState } from "react";
import Image from "next/image";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { ImagePlus, Trash2, Loader2 } from "lucide-react";
import { toast } from "sonner";

interface LogoUploadProps {
  /** Where to POST the image and DELETE it again. */
  endpoint: string;
  /** Stored path of the current logo, or null when none has been uploaded. */
  logoPath: string | null;
  /** Called after a successful upload or removal, with the new path. */
  onChange: (logoPath: string | null) => void;
  label: string;
  /** What this logo will be used for, in the reader's terms. */
  hint: string;
  disabled?: boolean;
  /** Table-cell sizing: a small preview and one button, no label or hint. */
  compact?: boolean;
}

export function logoUrl(path: string): string {
  return `/api/proxy/branding/file?path=${encodeURIComponent(path)}`;
}

/**
 * Upload the letterhead that heads this organisation's generated documents.
 *
 * Shows the current logo rather than a filename: the whole point is what it looks like at the top
 * of a printed page, and a filename tells you nothing about whether you picked the right file.
 */
export function LogoUpload({
  endpoint,
  logoPath,
  onChange,
  label,
  hint,
  disabled = false,
  compact = false,
}: LogoUploadProps) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [busy, setBusy] = useState(false);

  const upload = async (file: File) => {
    setBusy(true);
    try {
      const form = new FormData();
      form.append("file", file);
      const res = await fetch(endpoint, { method: "POST", body: form });
      const body = await res.json().catch(() => ({}));
      if (!res.ok || body?.isSuccess === false) {
        toast.error(body?.error || "That logo could not be saved.");
        return;
      }
      onChange(body?.data?.logoPath ?? null);
      toast.success("Logo updated");
    } catch {
      toast.error("That logo could not be saved.");
    } finally {
      setBusy(false);
      if (inputRef.current) inputRef.current.value = "";
    }
  };

  const remove = async () => {
    setBusy(true);
    try {
      const res = await fetch(endpoint, { method: "DELETE" });
      if (!res.ok) {
        toast.error("That logo could not be removed.");
        return;
      }
      onChange(null);
      toast.success("Logo removed");
    } catch {
      toast.error("That logo could not be removed.");
    } finally {
      setBusy(false);
    }
  };

  const box = compact ? "h-9 w-20" : "h-20 w-44";

  return (
    <div className={compact ? "flex items-center gap-2" : "space-y-2"}>
      {!compact && <Label>{label}</Label>}
      <div className={compact ? "flex items-center gap-2" : "flex items-center gap-4"}>
        <div
          className={`flex ${box} shrink-0 items-center justify-center overflow-hidden rounded-md border bg-muted/40`}
        >
          {logoPath ? (
            <Image
              src={logoUrl(logoPath)}
              alt={label || "Letterhead"}
              width={compact ? 80 : 176}
              height={compact ? 36 : 80}
              unoptimized
              className="max-h-full w-auto object-contain"
            />
          ) : (
            <span className="text-[10px] text-muted-foreground">No logo</span>
          )}
        </div>
        <div className={compact ? "" : "space-y-2"}>
          <div className="flex gap-2">
            <Button
              type="button"
              variant="outline"
              size="sm"
              disabled={disabled || busy}
              onClick={() => inputRef.current?.click()}
              aria-label={
                compact ? (logoPath ? "Replace letterhead" : "Upload letterhead") : undefined
              }
            >
              {busy ? (
                <Loader2 className={`h-3.5 w-3.5 animate-spin ${compact ? "" : "mr-1.5"}`} />
              ) : (
                <ImagePlus className={`h-3.5 w-3.5 ${compact ? "" : "mr-1.5"}`} />
              )}
              {compact ? "" : logoPath ? "Replace" : "Upload"}
            </Button>
            {logoPath && !compact && (
              <Button
                type="button"
                variant="ghost"
                size="sm"
                disabled={disabled || busy}
                onClick={remove}
              >
                <Trash2 className="mr-1.5 h-3.5 w-3.5" />
                Remove
              </Button>
            )}
          </div>
          {!compact && <p className="text-xs text-muted-foreground">{hint}</p>}
        </div>
      </div>
      <input
        ref={inputRef}
        type="file"
        accept="image/png,image/jpeg"
        className="hidden"
        onChange={(e) => {
          const file = e.target.files?.[0];
          if (file) void upload(file);
        }}
      />
    </div>
  );
}
