"use client";

import { useEffect, useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { toast } from "sonner";
import {
  commissionsApi,
  formatEtb,
  type CommissionFee,
  type FeeLineInput,
} from "@/lib/api/commissions";
import { Plus, Trash2 } from "lucide-react";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";

const FEE_TYPES = ["AgencyFee", "PartnerShare", "Medical", "Ticket", "Other"];

type DraftFee = FeeLineInput & { key: string };

function toDraft(fees: CommissionFee[]): DraftFee[] {
  if (fees.length === 0) {
    return [
      {
        key: crypto.randomUUID(),
        feeType: "AgencyFee",
        description: "",
        amount: 0,
        currency: "ETB",
        sortOrder: 0,
      },
    ];
  }
  return fees.map((f, i) => ({
    key: f.id,
    feeType: f.feeType,
    description: f.description ?? "",
    amount: f.amount,
    currency: f.currency || "ETB",
    sortOrder: f.sortOrder ?? i,
  }));
}

export function FeeBreakdownEditor({
  commissionId,
  fees,
  readOnly,
  onSaved,
}: {
  commissionId: string;
  fees: CommissionFee[];
  readOnly?: boolean;
  onSaved: () => void;
}) {
  const [rows, setRows] = useState<DraftFee[]>(() => toDraft(fees));
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    setRows(toDraft(fees));
  }, [fees]);

  const totalEtb = rows.reduce((sum, r) => sum + (Number(r.amount) || 0), 0);

  const save = async () => {
    setBusy(true);
    try {
      await commissionsApi.upsertFees(
        commissionId,
        rows.map((r, i) => ({
          feeType: r.feeType,
          description: r.description || null,
          amount: Number(r.amount) || 0,
          currency: r.currency || "ETB",
          sortOrder: i,
        }))
      );
      toast.success("Fees saved");
      onSaved();
    } catch (e) {
      toast.error(e instanceof Error ? e.message : "Failed to save fees");
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="space-y-3">
      <div className="overflow-x-auto rounded-md border">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Type</TableHead>
              <TableHead>Description</TableHead>
              <TableHead>Currency</TableHead>
              <TableHead className="text-right">Amount</TableHead>
              {!readOnly ? <TableHead className="w-10" /> : null}
            </TableRow>
          </TableHeader>
          <TableBody>
            {rows.map((row, idx) => (
              <TableRow key={row.key}>
                <TableCell className="px-2 py-1.5">
                  {readOnly ? (
                    row.feeType
                  ) : (
                    <Select
                      value={row.feeType}
                      onValueChange={(v) =>
                        setRows((prev) =>
                          prev.map((r, i) => (i === idx ? { ...r, feeType: v } : r))
                        )
                      }
                    >
                      <SelectTrigger className="h-8">
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        {FEE_TYPES.map((t) => (
                          <SelectItem key={t} value={t}>
                            {t}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  )}
                </TableCell>
                <TableCell className="px-2 py-1.5">
                  {readOnly ? (
                    row.description || "—"
                  ) : (
                    <Input
                      className="h-8"
                      value={row.description ?? ""}
                      onChange={(e) =>
                        setRows((prev) =>
                          prev.map((r, i) =>
                            i === idx ? { ...r, description: e.target.value } : r
                          )
                        )
                      }
                    />
                  )}
                </TableCell>
                <TableCell className="px-2 py-1.5">
                  {readOnly ? (
                    row.currency
                  ) : (
                    <Input
                      className="h-8 w-20"
                      value={row.currency ?? "ETB"}
                      onChange={(e) =>
                        setRows((prev) =>
                          prev.map((r, i) =>
                            i === idx ? { ...r, currency: e.target.value.toUpperCase() } : r
                          )
                        )
                      }
                    />
                  )}
                </TableCell>
                <TableCell className="px-2 py-1.5">
                  {readOnly ? (
                    <span className="block text-right tabular-nums">
                      {Number(row.amount).toFixed(2)}
                    </span>
                  ) : (
                    <Input
                      className="h-8 text-right"
                      type="number"
                      min={0}
                      step="0.01"
                      value={row.amount}
                      onChange={(e) =>
                        setRows((prev) =>
                          prev.map((r, i) =>
                            i === idx ? { ...r, amount: Number(e.target.value) } : r
                          )
                        )
                      }
                    />
                  )}
                </TableCell>
                {!readOnly ? (
                  <TableCell className="px-1 py-1.5">
                    <Button
                      type="button"
                      size="icon"
                      variant="ghost"
                      className="h-8 w-8"
                      disabled={rows.length <= 1}
                      onClick={() => setRows((prev) => prev.filter((_, i) => i !== idx))}
                    >
                      <Trash2 className="h-3.5 w-3.5" />
                    </Button>
                  </TableCell>
                ) : null}
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>

      <div className="flex flex-wrap items-center justify-between gap-2">
        <p className="text-sm text-muted-foreground">
          Line total (entered amounts): {formatEtb(totalEtb)}
          <span className="ml-1 text-xs">(ETB conversion applied on save)</span>
        </p>
        {!readOnly ? (
          <div className="flex gap-2">
            <Button
              type="button"
              size="sm"
              variant="outline"
              className="h-8 gap-1.5"
              onClick={() =>
                setRows((prev) => [
                  ...prev,
                  {
                    key: crypto.randomUUID(),
                    feeType: "Other",
                    description: "",
                    amount: 0,
                    currency: "ETB",
                    sortOrder: prev.length,
                  },
                ])
              }
            >
              <Plus className="h-3.5 w-3.5" />
              Add line
            </Button>
            <Button
              type="button"
              size="sm"
              className="h-8 bg-green-800 hover:bg-green-900 text-white"
              disabled={busy}
              onClick={save}
            >
              {busy ? "Saving…" : "Save fees"}
            </Button>
          </div>
        ) : null}
      </div>
    </div>
  );
}
