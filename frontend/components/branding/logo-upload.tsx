"use client";

import { useEffect, useRef, useState } from "react";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { ImagePlus, Trash2, Loader2 } from "lucide-react";
import { ImageLightbox } from "@/components/candidates/candidate-image";
import { toast } from "sonner";
import { brandingHeaders, logoUrl } from "@/lib/api/branding";

export { logoUrl };

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
  /** Preview above the buttons and as wide as the column, for side-by-side cards. */
  stacked?: boolean;
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
  stacked = false,
}: LogoUploadProps) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [busy, setBusy] = useState(false);
  const [zoomed, setZoomed] = useState(false);
  // A replacement gets a new filename, so a path that changes is a fresh image worth retrying.
  const [broken, setBroken] = useState(false);
  useEffect(() => setBroken(false), [logoPath]);

  const upload = async (file: File) => {
    setBusy(true);
    try {
      const form = new FormData();
      form.append("file", file);
      const res = await fetch(endpoint, { method: "POST", body: form, headers: brandingHeaders() });
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
      const res = await fetch(endpoint, { method: "DELETE", headers: brandingHeaders() });
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

  const box = compact ? "h-9 w-20" : stacked ? "h-24 w-full" : "h-20 w-44";

  return (
    <div className={compact ? "flex items-center gap-2" : "space-y-2"}>
      {!compact && <Label>{label}</Label>}
      <div
        className={
          compact
            ? "flex items-center gap-2"
            : stacked
              ? "space-y-2"
              : "flex items-center gap-4"
        }
      >
        {/* A plain img, not next/image: this one has to be able to say that it failed. A stored
            path whose file cannot be served used to render an empty frame indistinguishable from
            having uploaded nothing, which is exactly the case somebody needs to be told about. */}
        <button
          type="button"
          onClick={() => logoPath && !broken && setZoomed(true)}
          disabled={!logoPath || broken}
          title={logoPath && !broken ? "View full size" : undefined}
          className={`flex ${box} items-center justify-center overflow-hidden rounded-md border bg-muted/40 p-1 ${
            logoPath && !broken ? "cursor-zoom-in hover:border-primary/40" : "cursor-default"
          }`}
        >
          {logoPath && !broken ? (
            // eslint-disable-next-line @next/next/no-img-element
            <img
              src={logoUrl(logoPath)}
              alt={label || "Letterhead"}
              onError={() => setBroken(true)}
              className="max-h-full max-w-full object-contain"
            />
          ) : (
            <span className="px-1 text-center text-[10px] leading-tight text-muted-foreground">
              {broken ? "Image unavailable" : "Nothing uploaded"}
            </span>
          )}
        </button>
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
      {logoPath && (
        <ImageLightbox
          src={logoUrl(logoPath)}
          alt={label || "Letterhead"}
          open={zoomed}
          onOpenChange={setZoomed}
        />
      )}
    </div>
  );
}

/**
 * Picks an image before there is anything to attach it to.
 *
 * Registering a partner creates the record and its branding in one go, but the upload endpoint
 * needs an id that does not exist until the form is saved. So the file is held here and sent
 * once the partner exists — see uploadBranding below.
 */
export function PendingImagePicker({
  file,
  onPick,
  label,
  hint,
}: {
  file: File | null;
  onPick: (file: File | null) => void;
  label: string;
  hint?: string;
}) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [preview, setPreview] = useState<string | null>(null);
  const [zoomed, setZoomed] = useState(false);

  useEffect(() => {
    if (!file) {
      setPreview(null);
      return;
    }
    const url = URL.createObjectURL(file);
    setPreview(url);
    return () => URL.revokeObjectURL(url);
  }, [file]);

  return (
    <div className="space-y-1.5">
      <Label>{label}</Label>
      <div className="flex items-center gap-3">
        {/* Big enough to recognise, and it opens: a letterhead is a banner of small type, and at
            thumbnail size one agency's looks like another's. */}
        <button
          type="button"
          onClick={() => preview && setZoomed(true)}
          disabled={!preview}
          title={preview ? "View full size" : undefined}
          className={`flex h-20 w-36 shrink-0 items-center justify-center overflow-hidden rounded-md border bg-muted/40 p-1 ${
            preview ? "cursor-zoom-in hover:border-primary/40" : "cursor-default"
          }`}
        >
          {preview ? (
            // A blob URL from the file just chosen — next/image cannot optimise it.
            // eslint-disable-next-line @next/next/no-img-element
            <img src={preview} alt={label} className="max-h-full max-w-full object-contain" />
          ) : (
            <span className="text-[10px] text-muted-foreground">None</span>
          )}
        </button>
        <div className="space-y-1.5">
          <div className="flex gap-2">
            <Button type="button" variant="outline" size="sm" onClick={() => inputRef.current?.click()}>
              <ImagePlus className="mr-1.5 h-3.5 w-3.5" />
              {file ? "Change" : "Choose"}
            </Button>
            {file && (
              <Button
                type="button"
                variant="ghost"
                size="sm"
                onClick={() => {
                  onPick(null);
                  if (inputRef.current) inputRef.current.value = "";
                }}
              >
                <Trash2 className="mr-1.5 h-3.5 w-3.5" />
                Clear
              </Button>
            )}
          </div>
          {hint ? <p className="text-xs text-muted-foreground">{hint}</p> : null}
        </div>
      </div>
      <input
        ref={inputRef}
        type="file"
        accept="image/png,image/jpeg"
        className="hidden"
        onChange={(e) => onPick(e.target.files?.[0] ?? null)}
      />
      <ImageLightbox src={preview} alt={label} open={zoomed} onOpenChange={setZoomed} />
    </div>
  );
}

/**
 * Sends one branding image for an organisation that now exists.
 *
 * Deliberately does not throw: the partner has already been created by this point, and losing
 * that to a failed image upload would be worse than the partner briefly having no logo.
 */
export async function uploadBranding(
  endpoint: string,
  file: File
): Promise<boolean> {
  try {
    const form = new FormData();
    form.append("file", file);
    const res = await fetch(endpoint, { method: "POST", body: form, headers: brandingHeaders() });
    return res.ok;
  } catch {
    return false;
  }
}
