import type { CandidateListItem, TimelineItem } from "@/types/candidate";
import type { AvailableAction, Office, TrackStatusCell, WorkflowViewRow } from "@/types/workflow";

export const DEMO_STAGES = [
  { id: "stage-new-contracts", slug: "new-contracts", name: "New Contracts", order: 1 },
  { id: "stage-embassy", slug: "embassy", name: "Embassy", order: 2 },
  { id: "stage-lmis", slug: "lmis", name: "LMIS", order: 3 },
  { id: "stage-ticket", slug: "tickets", name: "Tickets", order: 4 },
  { id: "stage-depart", slug: "departures", name: "Departures", order: 5 },
  { id: "stage-arrival", slug: "arrivals", name: "Arrivals", order: 6 },
  { id: "stage-commission", slug: "commissions", name: "Commissions", order: 7 },
] as const;

export type DemoStageSlug = (typeof DEMO_STAGES)[number]["slug"];

export interface DemoCandidate {
  id: string;
  applicationNo: string;
  fullName: string;
  firstName: string;
  lastName: string;
  middleName?: string;
  passportNumber: string;
  labourId: string;
  age: number;
  gender: number;
  countryOfTravel: string;
  sponsorName: string;
  sponsorId?: string;
  visaNo?: string;
  agent?: string;
  officeId: string;
  officeName: string;
  stageSlug: DemoStageSlug;
  statusValues: Record<string, string>;
  enteredAt: string;
  lastActionAt: string;
  lastActionLabel: string;
  registeredAt: string;
  flightDate?: string;
  isPreview?: boolean;
  isOverdue?: boolean;
}

export const DEMO_OFFICES: Office[] = [
  {
    id: "11111111-1111-1111-1111-111111111001",
    name: "Head Office — Addis Ababa",
    code: "HO-ADD",
    city: "Addis Ababa",
    phone: "+251911000100",
    email: "headoffice@simbaflow.local",
    isActive: true,
  },
  {
    id: "11111111-1111-1111-1111-111111111002",
    name: "Bole Branch",
    code: "BR-BOLE",
    city: "Addis Ababa",
    phone: "+251911000200",
    email: "bole@simbaflow.local",
    isActive: true,
  },
  {
    id: "11111111-1111-1111-1111-111111111003",
    name: "Hawassa Office",
    code: "OF-HAW",
    city: "Hawassa",
    phone: "+251911000300",
    email: "hawassa@simbaflow.local",
    isActive: true,
  },
];

function daysBetween(iso: string) {
  return Math.max(0, Math.floor((Date.now() - new Date(iso).getTime()) / 86_400_000));
}

function remainingDays(flightDate?: string) {
  if (!flightDate) return undefined;
  return Math.ceil((new Date(flightDate).getTime() - Date.now()) / 86_400_000);
}

function track(key: string, status: string | undefined, since?: string): TrackStatusCell {
  return {
    trackKey: key,
    status: status || undefined,
    since,
    daysOnStep: since ? daysBetween(since) : 0,
  };
}

function actionsFor(c: DemoCandidate): AvailableAction[] {
  switch (c.stageSlug) {
    case "new-contracts":
      return [{ transitionRuleId: "tr-to-embassy", buttonLabel: "To Embassy", isEnabled: true }];
    case "embassy": {
      const canLmis = c.statusValues.embassy === "Issued";
      const locked = !(c.statusValues.medical === "Fit" && c.statusValues.tasheer === "Done");
      return [
        {
          transitionRuleId: "tr-to-lmis",
          buttonLabel: "To LMIS",
          isEnabled: canLmis,
          disabledReason: canLmis ? undefined : locked ? "Needs FIT + DONE" : "Requires embassy=Issued",
        },
      ];
    }
    case "lmis":
      if (c.isPreview) {
        return [
          {
            transitionRuleId: "tr-to-lmis",
            buttonLabel: "Transfer to LMIS",
            isEnabled: c.statusValues.embassy === "Issued",
            disabledReason: c.statusValues.embassy === "Issued" ? undefined : "Waiting on ISSUED",
          },
        ];
      }
      return [
        {
          transitionRuleId: "tr-to-ticket",
          buttonLabel: "To Ticket",
          isEnabled: c.statusValues.lmis === "Issued",
          disabledReason: c.statusValues.lmis === "Issued" ? undefined : "Requires LMIS Issued",
        },
      ];
    case "tickets":
      return [
        {
          transitionRuleId: "tr-to-depart",
          buttonLabel: "To Depart",
          isEnabled: c.statusValues.ticket === "Booked" && !!c.flightDate,
          disabledReason: "Requires booked ticket + flight date",
        },
      ];
    case "departures":
      return [
        {
          transitionRuleId: "tr-to-arrival",
          buttonLabel: "To Arrival",
          isEnabled: c.statusValues.depart === "Depart",
          disabledReason: "Mark DEPART first",
        },
      ];
    case "arrivals":
      return [
        {
          transitionRuleId: "tr-to-commission",
          buttonLabel: "Send to Commission",
          isEnabled: true,
        },
      ];
    case "commissions":
      return [];
    default:
      return [];
  }
}

function tracksFor(c: DemoCandidate): TrackStatusCell[] {
  const s = c.statusValues;
  switch (c.stageSlug) {
    case "embassy":
      return [
        track("medical", s.medical, s.medical_since),
        track("tasheer", s.tasheer, s.tasheer_since),
        track("embassy", s.embassy, s.embassy_since),
      ];
    case "lmis":
      if (c.isPreview) return [];
      return [track("insurance", s.insurance, s.insurance_since), track("lmis", s.lmis, s.lmis_since)];
    case "tickets":
      return [track("ticket", s.ticket, s.ticket_since)];
    case "departures":
      return [track("depart", s.depart, s.depart_since)];
    case "arrivals":
      return [track("arrival", s.arrival, s.arrival_since)];
    case "commissions":
      return [track("commission", s.commission, s.commission_since)];
    default:
      return [];
  }
}

const HO = DEMO_OFFICES[0]!;
const BOLE = DEMO_OFFICES[1]!;
const HAW = DEMO_OFFICES[2]!;

/** Rich demo population across every pipeline stage */
export const DEMO_CANDIDATES: DemoCandidate[] = [
  // New Contracts
  {
    id: "c-nc-01",
    applicationNo: "E821990011",
    fullName: "Selam Getachew Wolde",
    firstName: "Selam",
    lastName: "Wolde",
    middleName: "Getachew",
    passportNumber: "EQ3300221",
    labourId: "EF20001",
    age: 25,
    gender: 1,
    countryOfTravel: "KSA",
    sponsorName: "",
    officeId: BOLE.id,
    officeName: BOLE.name,
    stageSlug: "new-contracts",
    statusValues: {},
    enteredAt: "2026-07-20T08:00:00Z",
    lastActionAt: "2026-07-20T08:00:00Z",
    lastActionLabel: "Registered",
    registeredAt: "2026-07-20T08:00:00Z",
  },
  {
    id: "c-nc-02",
    applicationNo: "E821887744",
    fullName: "Betelhem Assefa Girma",
    firstName: "Betelhem",
    lastName: "Girma",
    middleName: "Assefa",
    passportNumber: "EP2244887",
    labourId: "EF20002",
    age: 27,
    gender: 1,
    countryOfTravel: "Kuwait",
    sponsorName: "",
    officeId: HO.id,
    officeName: HO.name,
    stageSlug: "new-contracts",
    statusValues: {},
    enteredAt: "2026-07-19T10:00:00Z",
    lastActionAt: "2026-07-19T10:00:00Z",
    lastActionLabel: "Registered",
    registeredAt: "2026-07-19T10:00:00Z",
  },
  {
    id: "c-nc-03",
    applicationNo: "E822001100",
    fullName: "Hiwot Alemayehu Bekele",
    firstName: "Hiwot",
    lastName: "Bekele",
    middleName: "Alemayehu",
    passportNumber: "EQ4411223",
    labourId: "EF20003",
    age: 23,
    gender: 1,
    countryOfTravel: "UAE",
    sponsorName: "",
    officeId: HAW.id,
    officeName: HAW.name,
    stageSlug: "new-contracts",
    statusValues: {},
    enteredAt: "2026-07-21T07:30:00Z",
    lastActionAt: "2026-07-21T07:30:00Z",
    lastActionLabel: "Registered",
    registeredAt: "2026-07-21T07:30:00Z",
  },

  // Embassy
  {
    id: "c-em-01",
    applicationNo: "E821241832",
    fullName: "Mekiya Yimer Kebede",
    firstName: "Mekiya",
    lastName: "Kebede",
    middleName: "Yimer",
    passportNumber: "EQ2623576",
    labourId: "EF10000",
    age: 22,
    gender: 1,
    countryOfTravel: "KSA",
    sponsorName: "Amjad Khayat",
    sponsorId: "1078324249",
    visaNo: "1907992593",
    agent: "Badawood",
    officeId: HO.id,
    officeName: HO.name,
    stageSlug: "embassy",
    statusValues: {
      medical: "Fit",
      medical_since: "2026-07-14T11:03:00Z",
      tasheer: "Done",
      tasheer_since: "2026-07-14T09:30:00Z",
      tasheer_datetime: "2026-07-14T09:30:00Z",
      embassy: "",
    },
    enteredAt: "2026-07-10T09:12:00Z",
    lastActionAt: "2026-07-14T11:03:00Z",
    lastActionLabel: "Medical → Fit",
    registeredAt: "2026-07-17T08:00:00Z",
  },
  {
    id: "c-em-02",
    applicationNo: "E820921905",
    fullName: "Zeynaba Mohammed Aleye",
    firstName: "Zeynaba",
    lastName: "Aleye",
    middleName: "Mohammed",
    passportNumber: "EP7420658",
    labourId: "EF10001",
    age: 33,
    gender: 1,
    countryOfTravel: "KSA",
    sponsorName: "Etenaa Resources",
    officeId: BOLE.id,
    officeName: BOLE.name,
    stageSlug: "embassy",
    statusValues: {
      medical: "OnProgress",
      medical_since: "2026-07-12T10:00:00Z",
      tasheer: "Booked",
      tasheer_since: "2026-07-11T14:00:00Z",
      tasheer_datetime: "2026-07-25T14:00:00Z",
      embassy: "",
    },
    enteredAt: "2026-07-08T10:00:00Z",
    lastActionAt: "2026-07-12T10:00:00Z",
    lastActionLabel: "Medical → OnProgress",
    registeredAt: "2026-07-13T08:00:00Z",
    isOverdue: true,
  },
  {
    id: "c-em-03",
    applicationNo: "E820465599",
    fullName: "Hawulet Muhammed Assen",
    firstName: "Hawulet",
    lastName: "Assen",
    middleName: "Muhammed",
    passportNumber: "EQ2014542",
    labourId: "EF10002",
    age: 29,
    gender: 1,
    countryOfTravel: "KSA",
    sponsorName: "Etenaa Resources",
    visaNo: "1306167510",
    agent: "Badawood",
    officeId: HO.id,
    officeName: HO.name,
    stageSlug: "embassy",
    statusValues: {
      medical: "Fit",
      medical_since: "2026-07-01T10:00:00Z",
      tasheer: "Done",
      tasheer_since: "2026-07-01T10:00:00Z",
      embassy: "Issued",
      embassy_since: "2026-07-15T14:00:00Z",
    },
    enteredAt: "2026-06-28T09:00:00Z",
    lastActionAt: "2026-07-15T14:00:00Z",
    lastActionLabel: "Embassy → Issued",
    registeredAt: "2026-07-06T08:00:00Z",
    isOverdue: true,
  },
  {
    id: "c-em-04",
    applicationNo: "E820112233",
    fullName: "Rahel Tadesse Mengistu",
    firstName: "Rahel",
    lastName: "Mengistu",
    middleName: "Tadesse",
    passportNumber: "EP9988771",
    labourId: "EF10010",
    age: 24,
    gender: 1,
    countryOfTravel: "Qatar",
    sponsorName: "Samood Al-khaleej",
    officeId: HAW.id,
    officeName: HAW.name,
    stageSlug: "embassy",
    statusValues: {
      medical: "Fit",
      medical_since: "2026-07-16T08:00:00Z",
      tasheer: "Done",
      tasheer_since: "2026-07-16T12:00:00Z",
      embassy: "Ready",
      embassy_since: "2026-07-18T09:00:00Z",
    },
    enteredAt: "2026-07-12T08:00:00Z",
    lastActionAt: "2026-07-18T09:00:00Z",
    lastActionLabel: "Embassy → Ready",
    registeredAt: "2026-07-11T08:00:00Z",
  },
  {
    id: "c-em-05",
    applicationNo: "E820556677",
    fullName: "Genet Hailu Kebede",
    firstName: "Genet",
    lastName: "Kebede",
    middleName: "Hailu",
    passportNumber: "EQ7766554",
    labourId: "EF10011",
    age: 26,
    gender: 1,
    countryOfTravel: "KSA",
    sponsorName: "Amjad Khayat",
    officeId: HO.id,
    officeName: HO.name,
    stageSlug: "embassy",
    statusValues: {
      medical: "Booked",
      medical_since: "2026-07-19T08:00:00Z",
      tasheer: "Booked",
      tasheer_since: "2026-07-19T08:00:00Z",
      embassy: "",
    },
    enteredAt: "2026-07-18T08:00:00Z",
    lastActionAt: "2026-07-19T08:00:00Z",
    lastActionLabel: "Medical booked",
    registeredAt: "2026-07-17T12:00:00Z",
  },

  // LMIS (full + preview)
  {
    id: "c-lm-01",
    applicationNo: "E819900011",
    fullName: "Aster Fikadu Worku",
    firstName: "Aster",
    lastName: "Worku",
    middleName: "Fikadu",
    passportNumber: "EP3344556",
    labourId: "EF10020",
    age: 28,
    gender: 1,
    countryOfTravel: "KSA",
    sponsorName: "Etenaa Resources",
    officeId: HO.id,
    officeName: HO.name,
    stageSlug: "lmis",
    statusValues: {
      insurance: "Paid",
      insurance_since: "2026-07-10T08:00:00Z",
      lmis: "Verified",
      lmis_since: "2026-07-16T10:00:00Z",
    },
    enteredAt: "2026-07-08T08:00:00Z",
    lastActionAt: "2026-07-16T10:00:00Z",
    lastActionLabel: "LMIS → Verified",
    registeredAt: "2026-06-20T08:00:00Z",
  },
  {
    id: "c-lm-02",
    applicationNo: "E819811122",
    fullName: "Tigist Belayneh Alemu",
    firstName: "Tigist",
    lastName: "Alemu",
    middleName: "Belayneh",
    passportNumber: "EQ5566778",
    labourId: "EF10021",
    age: 31,
    gender: 1,
    countryOfTravel: "Kuwait",
    sponsorName: "Samood Al-khaleej",
    officeId: BOLE.id,
    officeName: BOLE.name,
    stageSlug: "lmis",
    statusValues: {
      insurance: "Paid",
      insurance_since: "2026-07-12T08:00:00Z",
      lmis: "Issued",
      lmis_since: "2026-07-18T11:00:00Z",
    },
    enteredAt: "2026-07-05T08:00:00Z",
    lastActionAt: "2026-07-18T11:00:00Z",
    lastActionLabel: "LMIS → Issued",
    registeredAt: "2026-06-15T08:00:00Z",
  },
  {
    id: "c-lm-03",
    applicationNo: "E819700099",
    fullName: "Meron Desta Abate",
    firstName: "Meron",
    lastName: "Abate",
    middleName: "Desta",
    passportNumber: "EP6677889",
    labourId: "EF10022",
    age: 22,
    gender: 1,
    countryOfTravel: "UAE",
    sponsorName: "Golden Gate Partners",
    officeId: HAW.id,
    officeName: HAW.name,
    stageSlug: "lmis",
    statusValues: {
      insurance: "Unpaid",
      insurance_since: "2026-07-14T08:00:00Z",
      lmis: "Submitted",
      lmis_since: "2026-07-14T08:00:00Z",
    },
    enteredAt: "2026-07-14T08:00:00Z",
    lastActionAt: "2026-07-14T08:00:00Z",
    lastActionLabel: "LMIS submitted",
    registeredAt: "2026-07-01T08:00:00Z",
    isOverdue: true,
  },

  // Tickets
  {
    id: "c-tk-01",
    applicationNo: "E819318816",
    fullName: "Eman Tariku Liben",
    firstName: "Eman",
    lastName: "Liben",
    middleName: "Tariku",
    passportNumber: "EP9077840",
    labourId: "EF10003",
    age: 21,
    gender: 1,
    countryOfTravel: "KSA",
    sponsorName: "Etenaa Resources",
    officeId: HO.id,
    officeName: HO.name,
    stageSlug: "tickets",
    statusValues: { ticket: "Booked", ticket_since: "2026-07-16T09:00:00Z" },
    enteredAt: "2026-07-14T08:00:00Z",
    lastActionAt: "2026-07-16T09:00:00Z",
    lastActionLabel: "Ticket → Booked",
    registeredAt: "2026-06-22T08:00:00Z",
    flightDate: "2026-07-25T20:15:00Z",
  },
  {
    id: "c-tk-02",
    applicationNo: "E819200044",
    fullName: "Kidist Mulugeta Abebe",
    firstName: "Kidist",
    lastName: "Abebe",
    middleName: "Mulugeta",
    passportNumber: "EQ1122334",
    labourId: "EF10030",
    age: 27,
    gender: 1,
    countryOfTravel: "KSA",
    sponsorName: "Amjad Khayat",
    officeId: BOLE.id,
    officeName: BOLE.name,
    stageSlug: "tickets",
    statusValues: { ticket: "NotBooked", ticket_since: "2026-07-17T08:00:00Z" },
    enteredAt: "2026-07-17T08:00:00Z",
    lastActionAt: "2026-07-17T08:00:00Z",
    lastActionLabel: "Entered Ticket",
    registeredAt: "2026-06-28T08:00:00Z",
  },
  {
    id: "c-tk-03",
    applicationNo: "E819155500",
    fullName: "Yeshiwork Girma Tulu",
    firstName: "Yeshiwork",
    lastName: "Tulu",
    middleName: "Girma",
    passportNumber: "EP4455667",
    labourId: "EF10031",
    age: 30,
    gender: 1,
    countryOfTravel: "Bahrain",
    sponsorName: "Gulf Care Agency",
    officeId: HO.id,
    officeName: HO.name,
    stageSlug: "tickets",
    statusValues: { ticket: "Booked", ticket_since: "2026-07-19T12:00:00Z" },
    enteredAt: "2026-07-15T08:00:00Z",
    lastActionAt: "2026-07-19T12:00:00Z",
    lastActionLabel: "Ticket → Booked",
    registeredAt: "2026-06-10T08:00:00Z",
    flightDate: "2026-07-28T06:40:00Z",
  },

  // Departures
  {
    id: "c-dp-01",
    applicationNo: "E819159446",
    fullName: "Temire Arega Mekonnen",
    firstName: "Temire",
    lastName: "Mekonnen",
    middleName: "Arega",
    passportNumber: "EQ1169658",
    labourId: "EF10004",
    age: 21,
    gender: 1,
    countryOfTravel: "Kuwait",
    sponsorName: "Samood Al-khaleej",
    officeId: BOLE.id,
    officeName: BOLE.name,
    stageSlug: "departures",
    statusValues: { depart: "Notified", depart_since: "2026-07-18T08:00:00Z", ticket: "Booked" },
    enteredAt: "2026-07-16T08:00:00Z",
    lastActionAt: "2026-07-18T08:00:00Z",
    lastActionLabel: "Candidate notified",
    registeredAt: "2026-06-17T08:00:00Z",
    flightDate: "2026-07-23T20:15:00Z",
  },
  {
    id: "c-dp-02",
    applicationNo: "E818990011",
    fullName: "Frehiwot Solomon Desta",
    firstName: "Frehiwot",
    lastName: "Desta",
    middleName: "Solomon",
    passportNumber: "EP2233445",
    labourId: "EF10040",
    age: 25,
    gender: 1,
    countryOfTravel: "KSA",
    sponsorName: "Etenaa Resources",
    officeId: HO.id,
    officeName: HO.name,
    stageSlug: "departures",
    statusValues: { depart: "Depart", depart_since: "2026-07-20T05:00:00Z", ticket: "Booked" },
    enteredAt: "2026-07-12T08:00:00Z",
    lastActionAt: "2026-07-20T05:00:00Z",
    lastActionLabel: "Marked DEPART",
    registeredAt: "2026-06-01T08:00:00Z",
    flightDate: "2026-07-20T08:15:00Z",
  },
  {
    id: "c-dp-03",
    applicationNo: "E818877700",
    fullName: "Samrawit Ayele Negash",
    firstName: "Samrawit",
    lastName: "Negash",
    middleName: "Ayele",
    passportNumber: "EQ8899001",
    labourId: "EF10041",
    age: 23,
    gender: 1,
    countryOfTravel: "UAE",
    sponsorName: "Golden Gate Partners",
    officeId: HAW.id,
    officeName: HAW.name,
    stageSlug: "departures",
    statusValues: { depart: "NotDepart", depart_since: "2026-07-19T18:00:00Z", ticket: "Booked" },
    enteredAt: "2026-07-10T08:00:00Z",
    lastActionAt: "2026-07-19T18:00:00Z",
    lastActionLabel: "Not departed — reschedule",
    registeredAt: "2026-05-28T08:00:00Z",
    flightDate: "2026-07-19T22:00:00Z",
    isOverdue: true,
  },

  // Arrivals
  {
    id: "c-ar-01",
    applicationNo: "E819160357",
    fullName: "Worksew Kassahun",
    firstName: "Worksew",
    lastName: "Kassahun",
    passportNumber: "EP8594321",
    labourId: "EF10005",
    age: 26,
    gender: 1,
    countryOfTravel: "KSA",
    sponsorName: "Etenaa Resources",
    officeId: HO.id,
    officeName: HO.name,
    stageSlug: "arrivals",
    statusValues: { arrival: "OnDuty", arrival_since: "2026-07-02T10:00:00Z" },
    enteredAt: "2026-07-01T08:00:00Z",
    lastActionAt: "2026-07-02T10:00:00Z",
    lastActionLabel: "Arrival → OnDuty",
    registeredAt: "2026-05-15T08:00:00Z",
  },
  {
    id: "c-ar-02",
    applicationNo: "E818100200",
    fullName: "Birtukan Hailu Mekuria",
    firstName: "Birtukan",
    lastName: "Mekuria",
    middleName: "Hailu",
    passportNumber: "EQ3344556",
    labourId: "EF10050",
    age: 29,
    gender: 1,
    countryOfTravel: "KSA",
    sponsorName: "Amjad Khayat",
    officeId: BOLE.id,
    officeName: BOLE.name,
    stageSlug: "arrivals",
    statusValues: { arrival: "Returned", arrival_since: "2026-07-10T12:00:00Z" },
    enteredAt: "2026-06-20T08:00:00Z",
    lastActionAt: "2026-07-10T12:00:00Z",
    lastActionLabel: "Arrival → Returned",
    registeredAt: "2026-04-10T08:00:00Z",
  },
  {
    id: "c-ar-03",
    applicationNo: "E818050050",
    fullName: "Emebet Yohannes Feyisa",
    firstName: "Emebet",
    lastName: "Feyisa",
    middleName: "Yohannes",
    passportNumber: "EP7788990",
    labourId: "EF10051",
    age: 24,
    gender: 1,
    countryOfTravel: "Kuwait",
    sponsorName: "Samood Al-khaleej",
    officeId: HO.id,
    officeName: HO.name,
    stageSlug: "arrivals",
    statusValues: { arrival: "OnDuty", arrival_since: "2026-07-15T09:00:00Z" },
    enteredAt: "2026-07-14T08:00:00Z",
    lastActionAt: "2026-07-15T09:00:00Z",
    lastActionLabel: "Arrival confirmed",
    registeredAt: "2026-05-01T08:00:00Z",
  },

  // Commissions
  {
    id: "c-cm-01",
    applicationNo: "E817900001",
    fullName: "Almaz Bekele Tesfaye",
    firstName: "Almaz",
    lastName: "Tesfaye",
    middleName: "Bekele",
    passportNumber: "EQ9900112",
    labourId: "EF10060",
    age: 32,
    gender: 1,
    countryOfTravel: "KSA",
    sponsorName: "Etenaa Resources",
    officeId: HO.id,
    officeName: HO.name,
    stageSlug: "commissions",
    statusValues: { commission: "Requested", commission_since: "2026-07-12T08:00:00Z" },
    enteredAt: "2026-07-12T08:00:00Z",
    lastActionAt: "2026-07-12T08:00:00Z",
    lastActionLabel: "Sent to commission",
    registeredAt: "2026-03-20T08:00:00Z",
  },
  {
    id: "c-cm-02",
    applicationNo: "E817800002",
    fullName: "Sintayehu Gashaw Lemma",
    firstName: "Sintayehu",
    lastName: "Lemma",
    middleName: "Gashaw",
    passportNumber: "EP1010202",
    labourId: "EF10061",
    age: 28,
    gender: 1,
    countryOfTravel: "UAE",
    sponsorName: "Golden Gate Partners",
    officeId: HAW.id,
    officeName: HAW.name,
    stageSlug: "commissions",
    statusValues: { commission: "Paid", commission_since: "2026-07-18T14:00:00Z" },
    enteredAt: "2026-07-01T08:00:00Z",
    lastActionAt: "2026-07-18T14:00:00Z",
    lastActionLabel: "Commission → Paid",
    registeredAt: "2026-03-01T08:00:00Z",
  },
  {
    id: "c-cm-03",
    applicationNo: "E817700003",
    fullName: "Meseret Abdi Kelifa",
    firstName: "Meseret",
    lastName: "Kelifa",
    middleName: "Abdi",
    passportNumber: "EQ3030404",
    labourId: "EF10062",
    age: 27,
    gender: 1,
    countryOfTravel: "KSA",
    sponsorName: "Amjad Khayat",
    officeId: BOLE.id,
    officeName: BOLE.name,
    stageSlug: "commissions",
    statusValues: { commission: "Unpaid", commission_since: "2026-07-08T08:00:00Z" },
    enteredAt: "2026-07-08T08:00:00Z",
    lastActionAt: "2026-07-08T08:00:00Z",
    lastActionLabel: "Commission unpaid",
    registeredAt: "2026-02-15T08:00:00Z",
    isOverdue: true,
  },
];

/** Mirror preview rows for LMIS (embassy FIT+DONE still in embassy) */
function lmisPreviews(): DemoCandidate[] {
  return DEMO_CANDIDATES.filter(
    (c) =>
      c.stageSlug === "embassy" &&
      c.statusValues.medical === "Fit" &&
      c.statusValues.tasheer === "Done",
  ).map((c) => ({
    ...c,
    id: `${c.id}-preview`,
    stageSlug: "lmis" as DemoStageSlug,
    isPreview: true,
  }));
}

export function resolveStageSlug(stageIdOrSlug: string): string {
  const lower = stageIdOrSlug.toLowerCase();
  const byId = DEMO_STAGES.find((s) => s.id === lower || s.id === stageIdOrSlug);
  if (byId) return byId.slug;
  const bySlug = DEMO_STAGES.find(
    (s) => s.slug === lower || s.slug.replace(/s$/, "") === lower.replace(/s$/, ""),
  );
  return bySlug?.slug ?? lower;
}

export function toWorkflowViewRow(c: DemoCandidate): WorkflowViewRow {
  return {
    id: c.id,
    applicationNo: c.applicationNo,
    fullName: c.fullName,
    passportNumber: c.passportNumber,
    labourId: c.labourId,
    countryOfTravel: c.countryOfTravel,
    sponsorName: c.sponsorName || undefined,
    officeName: c.officeName,
    currentStatusValues: Object.fromEntries(
      Object.entries(c.statusValues).filter(([k]) => !k.endsWith("_since") && k !== "tasheer_datetime"),
    ),
    tracks: tracksFor(c),
    enteredAt: c.enteredAt,
    daysInStage: daysBetween(c.enteredAt),
    lastActionAt: c.lastActionAt,
    lastActionLabel: c.lastActionLabel,
    isOverdue: !!c.isOverdue || (daysBetween(c.enteredAt) > 10 && c.stageSlug !== "arrivals" && c.stageSlug !== "commissions"),
    isPreview: c.isPreview,
    flightDate: c.flightDate,
    remainingDays: remainingDays(c.flightDate),
    availableActions: actionsFor(c),
  };
}

export function getStageView(stageIdOrSlug: string) {
  const slug = resolveStageSlug(stageIdOrSlug);
  const stage = DEMO_STAGES.find((s) => s.slug === slug);
  let rows = DEMO_CANDIDATES.filter((c) => c.stageSlug === slug);
  if (slug === "lmis") {
    rows = [...lmisPreviews(), ...rows];
  }
  return {
    stageId: stage?.id ?? stageIdOrSlug,
    stageName: stage?.name ?? stageIdOrSlug,
    slug,
    items: rows.map(toWorkflowViewRow),
  };
}

export function getCandidateList(search?: string): CandidateListItem[] {
  let list = DEMO_CANDIDATES.map((c) => {
    const stage = DEMO_STAGES.find((s) => s.slug === c.stageSlug);
    return {
      id: c.id,
      applicationNo: c.applicationNo,
      fullName: c.fullName,
      passportNumber: c.passportNumber,
      labourId: c.labourId,
      currentStageId: stage?.id,
      currentStageName: stage?.name,
      currentStatusValues: c.statusValues,
      countryOfTravel: c.countryOfTravel,
      officeName: c.officeName,
      officeId: c.officeId,
      registeredAt: c.registeredAt,
      currentStageEnteredAt: c.enteredAt,
      daysInStage: daysBetween(c.enteredAt),
      lastActionAt: c.lastActionAt,
      lastActionLabel: c.lastActionLabel,
      isOverdue: !!c.isOverdue,
      flightDate: c.flightDate,
      status: 0,
    } satisfies CandidateListItem;
  });

  if (search) {
    const q = search.toLowerCase();
    list = list.filter(
      (c) =>
        c.fullName.toLowerCase().includes(q) ||
        c.passportNumber.toLowerCase().includes(q) ||
        (c.labourId ?? "").toLowerCase().includes(q) ||
        c.applicationNo.toLowerCase().includes(q) ||
        (c.currentStageName ?? "").toLowerCase().includes(q),
    );
  }
  return list;
}

export function getPipelineCounts() {
  return DEMO_STAGES.map((s) => ({
    ...s,
    count:
      s.slug === "lmis"
        ? DEMO_CANDIDATES.filter((c) => c.stageSlug === "lmis").length + lmisPreviews().length
        : DEMO_CANDIDATES.filter((c) => c.stageSlug === s.slug).length,
    overdue: DEMO_CANDIDATES.filter((c) => c.stageSlug === s.slug && c.isOverdue).length,
  }));
}

export function getDemoTimeline(candidateId: string): TimelineItem[] {
  const c = DEMO_CANDIDATES.find((x) => x.id === candidateId || `${x.id}-preview` === candidateId);
  if (!c) return [];
  return [
    {
      id: "t1",
      eventType: 0,
      eventTypeName: "Registered",
      toStageName: "New Contracts",
      userName: "sara.t",
      timestamp: c.registeredAt,
    },
    {
      id: "t2",
      eventType: 1,
      eventTypeName: "StageTransitioned",
      fromStageName: "New Contracts",
      toStageName: DEMO_STAGES.find((s) => s.slug === c.stageSlug)?.name,
      durationLabel: `${daysBetween(c.enteredAt)}d in stage`,
      userName: "yonas.b",
      timestamp: c.enteredAt,
    },
    {
      id: "t3",
      eventType: 2,
      eventTypeName: "StatusUpdated",
      trackKey: Object.keys(c.statusValues)[0],
      toStatus: Object.values(c.statusValues)[0],
      userName: "sara.t",
      timestamp: c.lastActionAt,
      notes: c.lastActionLabel,
    },
  ];
}

export function getCandidateDetail(id: string) {
  const baseId = id.replace(/-preview$/, "");
  const c = DEMO_CANDIDATES.find((x) => x.id === baseId);
  if (!c) return null;
  const stage = DEMO_STAGES.find((s) => s.slug === c.stageSlug);
  return {
    ...c,
    fullName: c.fullName,
    currentStageId: stage?.id,
    currentStageName: stage?.name,
    currentStatusValues: c.statusValues,
    daysInStage: daysBetween(c.enteredAt),
    currentStageEnteredAt: c.enteredAt,
    dateOfBirth: "1998-01-15",
    nationality: "Ethiopia",
    phoneNumber: "+251911000999",
    placement: {
      worksIn: c.countryOfTravel,
      countryOfTravel: c.countryOfTravel,
      sponsorName: c.sponsorName,
      sponsorId: c.sponsorId,
      visaNumber: c.visaNo,
      agent: c.agent,
    },
    skills: { englishLevel: "Basic", arabicLevel: "Intermediate", canClean: true, canCook: true, canIron: true },
    relatives: [{ relativeName: "Family Contact", relativePhone: "+251922000111", relativeKinship: "Father" }],
    documents: [],
    timeline: getDemoTimeline(c.id),
  };
}
