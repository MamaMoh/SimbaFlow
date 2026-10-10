export interface Candidate {
  id: string;
  firstName: string;
  lastName: string;
  middleName?: string;
  localFullName?: string;
  passportNumber: string;
  labourId?: string;
  biometricId?: string;
  nationalId?: string;
  dateOfBirth: string;
  placeOfBirth?: string;
  gender: number;
  nationality?: string;
  religion?: string;
  maritalStatus?: string;
  numberOfChildren?: number;
  height?: string;
  weight?: string;
  passportType?: string;
  passportPlaceOfIssue?: string;
  passportIssueDate?: string;
  passportExpiryDate?: string;
  phoneNumber?: string;
  email?: string;
  address?: string;
  city?: string;
  country?: string;
  region?: string;
  subcity?: string;
  woreda?: string;
  houseNo?: string;
  occupation?: string;
  qualification?: string;
  monthlySalary?: string;
  contractPeriod?: string;
  englishLevel?: string;
  arabicLevel?: string;
  otherLanguages?: string;
  /** Summed from workExperiences when the form saves. */
  experienceAbroadYears?: number;
  /** Every country in workExperiences, comma-separated. */
  worksIn?: string;
  workExperiences?: CandidateWorkExperience[];
  remark?: string;
  cookingLevel?: string;
  skillCleaning?: boolean;
  skillWashing?: boolean;
  skillCooking?: boolean;
  skillIroning?: boolean;
  skillSewing?: boolean;
  skillArabicCooking?: boolean;
  skillTutoring?: boolean;
  skillComputer?: boolean;
  complexion?: string;
  skillBabysitting?: boolean;
  skillChildCare?: boolean;
  extraSkills?: string[];
  countryOfTravel?: string;
  partnerName?: string;
  partnerAgencyId?: string | null;
  contractDate?: string;
  photoPath?: string;
  fullPhotoPath?: string;
  visaNumber?: string;
  visaType?: string;
  sponsorName?: string;
  sponsorIdNumber?: string;
  sponsorPhone?: string;
  sponsorAddress?: string;
  sponsorArabicName?: string;
  agentName?: string;
  /** The E number from the Saudi consular application — barcodes onto the enjaze form. */
  eNumber?: string;
  fileNo?: string;
  wakalaNo?: string;
  contractNo?: string;
  stickerVisaNo?: string;
  signedOn?: string;
  relativeName?: string;
  relativePhone?: string;
  relativeKinship?: string;
  relativeGender?: string;
  relativeBirthDate?: string;
  relativeCity?: string;
  relativeRegion?: string;
  relativeSubcity?: string;
  relativeWoreda?: string;
  relativeHouseNo?: string;
  contactPerson2?: string;
  contactPhone2?: string;
  cocCenterName?: string;
  certificateNo?: string;
  certifiedDate?: string;
  medicalPlace?: string;
  status: number;
  currentStageId?: string;
  currentStageName?: string;
  currentStatusValues?: Record<string, string>;
  /** What the enjaze form is still waiting for. Empty or absent means it can be printed. */
  visaFormMissing?: string[];
  visibleInStages?: string[];
  registeredAt: string;
  registeredBy?: string;
  fullName: string;
}

/** One posting abroad. A candidate can have several. */
export interface CandidateWorkExperience {
  country: string;
  occupation?: string | null;
  years?: number | null;
}

export interface CandidateListDto {
  id: string;
  fullName: string;
  passportNumber: string;
  labourId?: string;
  currentStageName?: string;
  currentStatusValues?: Record<string, string>;
  countryOfTravel?: string;
  partnerName?: string;
  status: string;
  registeredAt: string;
  dateOfBirth?: string;
  age?: number;
  occupation?: string;
  sponsorName?: string;
  sponsorIdNumber?: string;
  visaNumber?: string;
  agentName?: string;
  worksIn?: string;
  phoneNumber?: string;
  contactPerson2?: string;
  contactPhone2?: string;
  experienceAbroadYears?: number;
  /** Set when the record was removed; the Inactive list shows both. */
  deletedBy?: string;
  deletedAt?: string;
  /** False once the candidate has moved past the first stage — another desk holds them. */
  canDelete?: boolean;
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
  userName: string;
  timestamp: string;
  notes?: string;
}
