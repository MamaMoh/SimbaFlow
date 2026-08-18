"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { uploadCandidateDocument } from "@/lib/api/candidates";
import { toast } from "sonner";
import { Loader2, Upload } from "lucide-react";

const DOCUMENT_TYPES = [
  { value: 0, label: "Passport" },
  { value: 1, label: "Photo" },
  { value: 8, label: "Full Size Photo" },
  { value: 2, label: "Contract" },
  { value: 3, label: "CV" },
  { value: 9, label: "Visa Form" },
  { value: 4, label: "LMIS" },
  { value: 5, label: "Medical Certificate" },
  { value: 6, label: "Tasheer Document" },
  { value: 7, label: "Ticket Booking" },
  { value: 99, label: "Other" },
];

const ALLOWED_EXT = [".pdf", ".jpg", ".jpeg", ".png", ".docx"];
const MAX_BYTES = 10 * 1024 * 1024;

type DocumentUploaderProps = {
  candidateId: string;
  onUploaded?: () => void;
  /** Defaults to Passport (0). Pass 4 for LMIS. */
  defaultDocumentType?: number;
};

export function DocumentUploader({
  candidateId,
  onUploaded,
  defaultDocumentType = 0,
}: DocumentUploaderProps) {
  const [documentType, setDocumentType] = useState(String(defaultDocumentType));
  const [uploading, setUploading] = useState(false);

  const handleFile = async (file: File | undefined) => {
    if (!file) return;

    const ext = file.name.slice(file.name.lastIndexOf(".")).toLowerCase();
    if (!ALLOWED_EXT.includes(ext)) {
      toast.error(`File type not allowed. Use: ${ALLOWED_EXT.join(", ")}`);
      return;
    }
    if (file.size > MAX_BYTES) {
      toast.error("File exceeds 10MB limit");
      return;
    }

    setUploading(true);
    try {
      await uploadCandidateDocument(candidateId, file, Number(documentType));
      toast.success("Document uploaded");
      onUploaded?.();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Upload failed");
    } finally {
      setUploading(false);
    }
  };

  return (
    <div className="space-y-3 rounded-lg border p-4" data-testid="document-uploader">
      <div className="space-y-1.5">
        <Label>Document type</Label>
        <Select value={documentType} onValueChange={setDocumentType}>
          <SelectTrigger data-testid="document-type-select">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {DOCUMENT_TYPES.map((t) => (
              <SelectItem key={t.value} value={String(t.value)}>
                {t.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      <label
        className="flex cursor-pointer flex-col items-center justify-center gap-2 rounded-md border border-dashed px-4 py-8 text-sm text-muted-foreground hover:bg-muted/40"
        onDragOver={(e) => e.preventDefault()}
        onDrop={(e) => {
          e.preventDefault();
          handleFile(e.dataTransfer.files?.[0]);
        }}
      >
        {uploading ? (
          <Loader2 className="h-5 w-5 animate-spin" />
        ) : (
          <Upload className="h-5 w-5" />
        )}
        <span>Drop a file here or click to browse</span>
        <span className="text-xs">PDF, JPG, PNG, DOCX · max 10MB</span>
        <input
          type="file"
          className="hidden"
          accept=".pdf,.jpg,.jpeg,.png,.docx"
          disabled={uploading}
          onChange={(e) => handleFile(e.target.files?.[0])}
        />
      </label>

      <Button
        type="button"
        size="sm"
        className="bg-green-800 text-white hover:bg-green-900"
        disabled={uploading}
        onClick={() => {
          const input = document.querySelector<HTMLInputElement>(
            '[data-testid="document-uploader"] input[type="file"]'
          );
          input?.click();
        }}
      >
        Choose file
      </Button>
    </div>
  );
}
