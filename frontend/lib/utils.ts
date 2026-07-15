import { clsx, type ClassValue } from "clsx";
import { twMerge } from "tailwind-merge";

import type { User } from "@/types/auth";

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}

export function toSentenceCase(str: string) {
  return str
    .replace(/([A-Z])/g, " $1")
    .replace(/^./, (str) => str.toUpperCase());
}

export function convertBase64ToPdf(base64string: string): Blob {
  const binaryString = atob(base64string);

  const bytes = new Uint8Array(binaryString.length);
  for (let i = 0; i < binaryString.length; i++) {
    bytes[i] = binaryString.charCodeAt(i);
  }
  const blob = new Blob([bytes.buffer], { type: "application/pdf" });

  return blob;
}

export type recordStatus = 1 | 2 | 3;

export function getRecordStatusColor({
  status,
  shade = 600,
}: {
  status: recordStatus;
  shade?: 100 | 200 | 300 | 400 | 500 | 600 | 700 | 800 | 900 | 950;
}) {
  const bg = `bg-${shade}`;

  return cn({
    [`${bg}-yellow`]: status === 1, //inactive
    [`bg-green-${shade}`]: status === 2, //active
    [`${bg}-red`]: status === 3, //deleted
  });
}

export const RecordStatuses: {
  label: string;
  value: number;
}[] = [
  { label: "Active", value: 1 },
  { label: "Inactive", value: 0 },
];

// Backend sends: 1=Inactive, 2=Active, 3=Deleted
export function getRecordStatusMeta(status: number): {
  label: string;
  badgeVariant: "default" | "secondary" | "destructive";
} {
  switch (status) {
    case 1:
      return { label: "Inactive", badgeVariant: "secondary" };
    case 2:
      return { label: "Active", badgeVariant: "default" };
    case 3:
      return { label: "Deleted", badgeVariant: "destructive" };
    default:
      return { label: "Unknown", badgeVariant: "secondary" };
  }
}

export function computeEffectiveClaims(user: User): string[] {
  // Simply return the granted claims from JWT
  return user.grantedClaims || [];
}

//date formatter
export function formatDateTime(date: string | Date) {
  const d = typeof date === "string" ? new Date(date) : date;
  return d.toLocaleString(undefined, {
    year: "numeric",
    month: "short",
    day: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}
