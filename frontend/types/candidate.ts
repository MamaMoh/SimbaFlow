export interface Candidate {
  id: string;
  firstName: string;
  lastName: string;
  middleName?: string;
  passportNumber: string;
  labourId?: string;
  dateOfBirth: string;
  gender: number;
  nationality?: string;
  phoneNumber?: string;
  email?: string;
  address?: string;
  city?: string;
  country?: string;
  countryOfTravel?: string;
  officeName?: string;
  contractDate?: string;
  officeId: string;
  photoPath?: string;
  status: number;
  currentStageId?: string;
  currentStageName?: string;
  currentStatusValues?: Record<string, string>;
  visibleInStages?: string[];
  registeredAt: string;
  registeredBy?: string;
  fullName: string;
}

export interface CandidateListDto {
  id: string;
  fullName: string;
  passportNumber: string;
  labourId?: string;
  currentStageName?: string;
  currentStatusValues?: Record<string, string>;
  countryOfTravel?: string;
  officeName?: string;
  registeredAt: string;
}

export interface CandidateDocument {
  id: string;
  candidateId: string;
  fileName: string;
  originalFileName: string;
  contentType: string;
  filePath: string;
  thumbnailPath?: string;
  documentType: number;
  fileSizeBytes: number;
  uploadedAt: string;
  uploadedBy?: string;
}

export interface TimelineEntry {
  id: string;
  eventType: number;
  fromStageName?: string;
  toStageName?: string;
  data: Record<string, unknown>;
  userName: string;
  timestamp: string;
  notes?: string;
}
