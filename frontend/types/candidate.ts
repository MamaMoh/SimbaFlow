export interface CandidateListItem {
  id: string;
  applicationNo: string;
  fullName: string;
  passportNumber: string;
  labourId?: string;
  currentStageId?: string;
  currentStageName?: string;
  currentStatusValues?: Record<string, string>;
  countryOfTravel?: string;
  officeName?: string;
  officeId: string;
  registeredAt: string;
  currentStageEnteredAt?: string;
  daysInStage?: number;
  lastActionAt?: string;
  lastActionLabel?: string;
  isOverdue?: boolean;
  flightDate?: string;
  status: number;
}

export interface CandidatePlacementDto {
  countryOfTravel?: string;
  worksIn?: string;
  partnerId?: string;
  visaNumber?: string;
  visaType?: string;
  stickerVisaNumber?: string;
  sponsorId?: string;
  sponsorName?: string;
  sponsorNameArabic?: string;
  sponsorPhone?: string;
  sponsorAddress?: string;
  sponsorEmail?: string;
  agent?: string;
  nationalId?: string;
  contractNumber?: string;
  wakalaNumber?: string;
  signedOn?: string;
  contractDate?: string;
  cocCenter?: string;
  certifiedDate?: string;
  certificateNumber?: string;
  trainingType?: string;
  salary?: number;
  referenceNumber?: string;
  remarks?: string;
}

export interface CandidateRelativeDto {
  id?: string;
  relativeName: string;
  relativePhone?: string;
  relativeKinship?: string;
  gender?: number;
  birthDate?: string;
  region?: string;
  city?: string;
  subcity?: string;
  woreda?: string;
  houseNo?: string;
}

export interface CandidateSkillsDto {
  englishLevel?: string;
  arabicLevel?: string;
  experienceAbroad?: string;
  childrenCount?: number;
  height?: number;
  weight?: number;
  cookingNotes?: string;
  canIron?: boolean;
  canSew?: boolean;
  canBabysit?: boolean;
  canChildcare?: boolean;
  canArabicCooking?: boolean;
  canClean?: boolean;
  canWash?: boolean;
  canCook?: boolean;
}

export interface CandidateDocumentDto {
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

export interface CandidateDetail extends CandidateListItem {
  firstName: string;
  lastName: string;
  middleName?: string;
  fileNumber?: string;
  passportType?: string;
  passportIssueDate?: string;
  passportExpiryDate?: string;
  placeOfIssue?: string;
  placeOfBirth?: string;
  biometricId?: string;
  nationalId?: string;
  dateOfBirth: string;
  gender: number;
  nationality?: string;
  religion?: string;
  maritalStatus?: string;
  occupation?: string;
  qualification?: string;
  phoneNumber?: string;
  phone2?: string;
  email?: string;
  address?: string;
  city?: string;
  country?: string;
  region?: string;
  subcity?: string;
  woreda?: string;
  houseNo?: string;
  contractDate?: string;
  photoPath?: string;
  visibleInStages?: string[];
  registeredBy?: string;
  placement?: CandidatePlacementDto;
  skills?: CandidateSkillsDto;
  relatives?: CandidateRelativeDto[];
  documents?: CandidateDocumentDto[];
}

export interface TimelineItem {
  id: string;
  eventType: number;
  eventTypeName: string;
  fromStageName?: string;
  toStageName?: string;
  trackKey?: string;
  fromStatus?: string;
  toStatus?: string;
  durationMs?: number;
  durationLabel?: string;
  userName: string;
  timestamp: string;
  notes?: string;
  data?: Record<string, unknown>;
}

/** @deprecated use CandidateDetail */
export type Candidate = CandidateDetail;
/** @deprecated use CandidateListItem */
export type CandidateListDto = CandidateListItem;
/** @deprecated use CandidateDocumentDto */
export type CandidateDocument = CandidateDocumentDto;
/** @deprecated use TimelineItem */
export type TimelineEntry = TimelineItem;
