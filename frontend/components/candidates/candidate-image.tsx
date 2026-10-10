"use client";

import { useEffect, useState } from "react";
import { Dialog, DialogContent, DialogTitle } from "@/components/ui/dialog";
import { Loader2, Maximize2 } from "lucide-react";
import { cn } from "@/lib/utils";

/**
 * Fetches an image the API serves behind the session cookie and hands back a data URL.
 *
 * A plain <img src="/api/proxy/…"> would work, but the content-security policy allows data: and
 * not blob:, and a 404 on a candidate with no photo renders as a broken-image icon rather than
 * as nothing. Fetching first means a missing photo is simply absent.
 */
function useImageData(url: string | null) {
  const [data, setData] = useState<string | null>(null);
  const [state, setState] = useState<"idle" | "loading" | "ready" | "missing">("idle");

  useEffect(() => {
    if (!url) {
      setData(null);
      setState("idle");
      return;
    }

    let cancelled = false;
    setState("loading");
    setData(null);

    (async () => {
      try {
        const res = await fetch(url, { cache: "no-store" });
        if (!res.ok) throw new Error(String(res.status));
        const blob = await res.blob();
        if (blob.size === 0) throw new Error("empty");
        const asData = await new Promise<string>((resolve, reject) => {
          const reader = new FileReader();
          reader.onload = () =>
            typeof reader.result === "string" ? resolve(reader.result) : reject(new Error("read"));
          reader.onerror = () => reject(reader.error ?? new Error("read"));
          reader.readAsDataURL(blob);
        });
        if (cancelled) return;
        setData(asData);
        setState("ready");
      } catch {
        if (!cancelled) {
          setData(null);
          setState("missing");
        }
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [url]);

  return { data, state };
}

/**
 * An image at full size, over the page.
 *
 * A passport biodata page in a thumbnail is a grey rectangle — the number, the dates and the
 * name on it are the things most often being checked, and none of them are legible until it is
 * big. So every candidate image on the form and on the profile opens.
 */
export function ImageLightbox({
  src,
  alt,
  open,
  onOpenChange,
}: {
  src: string | null;
  alt: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  return (
    <Dialog open={open && !!src} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[92vh] max-w-4xl overflow-auto p-3">
        <DialogTitle className="sr-only">{alt}</DialogTitle>
        {src ? (
          // eslint-disable-next-line @next/next/no-img-element
          <img src={src} alt={alt} className="mx-auto max-h-[84vh] w-auto object-contain" />
        ) : null}
      </DialogContent>
    </Dialog>
  );
}

/**
 * One of a candidate's images on the profile page: a thumbnail that opens.
 *
 * Nothing at all when there is no such image — an empty frame captioned "Passport" tells the
 * desk a file exists when none does.
 */
export function CandidateImage({
  url,
  label,
  className,
}: {
  url: string;
  label: string;
  className?: string;
}) {
  const { data, state } = useImageData(url);
  const [open, setOpen] = useState(false);

  if (state === "missing" || (state === "ready" && !data)) return null;

  return (
    <div className={cn("space-y-1.5", className)}>
      <button
        type="button"
        onClick={() => setOpen(true)}
        disabled={!data}
        aria-label={`View ${label} full size`}
        className="group relative flex h-40 w-full items-center justify-center overflow-hidden rounded-lg border bg-slate-50"
      >
        {state === "loading" ? (
          <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
        ) : data ? (
          <>
            {/* eslint-disable-next-line @next/next/no-img-element */}
            <img src={data} alt={label} className="h-full w-full bg-white object-contain" />
            <span className="absolute inset-0 flex items-center justify-center bg-slate-900/0 opacity-0 transition group-hover:bg-slate-900/35 group-hover:opacity-100">
              <Maximize2 className="h-5 w-5 text-white" />
            </span>
          </>
        ) : null}
      </button>
      <p className="text-xs font-medium text-muted-foreground">{label}</p>
      <ImageLightbox src={data} alt={label} open={open} onOpenChange={setOpen} />
    </div>
  );
}
