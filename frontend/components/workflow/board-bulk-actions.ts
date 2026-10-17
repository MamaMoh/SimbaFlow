import { CalendarClock, CircleSlash, ClipboardCheck, ShieldCheck, ShieldOff, Stethoscope } from "lucide-react";
import type { BulkAction } from "@/components/workflow/bulk-actions-menu";
import { embassyBulkApi, lmisBulkApi } from "@/lib/api/bulk-actions";
import type { EmbassyBoardRow } from "@/lib/api/embassy";
import type { LmisBoardRow } from "@/lib/api/lmis";
import {
  bookMedicalBlock,
  bookTasheerBlock,
  markInsurancePaidBlock,
  markInsuranceUnpaidBlock,
  recordMedicalResultBlock,
  recordTasheerResultBlock,
  unbookMedicalBlock,
} from "@/lib/workflow/board-action-rules";

const notes = { name: "notes", label: "Notes", type: "textarea" as const };

const medical = (row: EmbassyBoardRow) => row.statusValues?.medical ?? "";
const tasheer = (row: EmbassyBoardRow) => row.statusValues?.tasheer ?? "";

/** The Embassy board's Actions menu: the medical and Tasheer steps of each row's ⋯ menu. */
export const EMBASSY_BULK_ACTIONS: BulkAction<EmbassyBoardRow>[] = [
  {
    key: "book-medical",
    label: "Book medical",
    icon: Stethoscope,
    done: "Medical booked",
    block: (row) => bookMedicalBlock(medical(row)),
    run: (ids) => embassyBulkApi.bookMedical(ids),
  },
  {
    key: "unbook-medical",
    label: "Mark medical unbooked",
    icon: CircleSlash,
    done: "Medical marked unbooked",
    block: (row) => unbookMedicalBlock(medical(row)),
    run: (ids) => embassyBulkApi.unbookMedical(ids),
  },
  {
    key: "medical-result",
    label: "Record medical result",
    icon: ClipboardCheck,
    done: "Medical result recorded",
    block: (row) => recordMedicalResultBlock(medical(row)),
    fields: [
      {
        name: "result",
        label: "Result",
        type: "select",
        required: true,
        options: [
          { value: "Fit", label: "Fit" },
          { value: "Unfit", label: "Unfit" },
        ],
      },
      notes,
    ],
    run: (ids, v) => embassyBulkApi.recordMedicalResult(ids, v.result, v.notes),
  },
  {
    key: "book-tasheer",
    label: "Book Tasheer",
    icon: CalendarClock,
    done: "Tasheer booked",
    block: (row) => bookTasheerBlock(tasheer(row)),
    fields: [{ name: "appointmentDate", label: "Appointment date", type: "date", required: true }, notes],
    run: (ids, v) => embassyBulkApi.bookTasheer(ids, v.appointmentDate, v.notes),
  },
  {
    key: "tasheer-result",
    label: "Record Tasheer result",
    icon: ClipboardCheck,
    done: "Tasheer result recorded",
    block: (row) => recordTasheerResultBlock(tasheer(row)),
    fields: [
      {
        name: "result",
        label: "Result",
        type: "select",
        required: true,
        options: [
          { value: "Book Done", label: "Book Done" },
          { value: "Expired", label: "Expired" },
        ],
      },
      notes,
    ],
    run: (ids, v) => embassyBulkApi.recordTasheerResult(ids, v.result, v.notes),
  },
];

const insurance = (row: LmisBoardRow) => row.insurance ?? row.statusValues?.insurance ?? "";

/** The LMIS board's Actions menu: insurance, as on each row's ⋯ menu. */
export const LMIS_BULK_ACTIONS: BulkAction<LmisBoardRow>[] = [
  {
    key: "insurance-paid",
    label: "Mark insurance paid",
    icon: ShieldCheck,
    done: "Insurance marked paid",
    block: (row) => markInsurancePaidBlock(insurance(row)),
    fields: [notes],
    run: (ids, v) => lmisBulkApi.markInsurancePaid(ids, v.notes),
  },
  {
    key: "insurance-unpaid",
    label: "Mark insurance unpaid",
    icon: ShieldOff,
    done: "Insurance marked unpaid",
    block: (row) => markInsuranceUnpaidBlock(insurance(row)),
    fields: [notes],
    run: (ids, v) => lmisBulkApi.markInsuranceUnpaid(ids, v.notes),
  },
];
