import { z } from "zod";

// Schema for creating/updating staff (form input)
export const StaffSchema = z.object({
  staffId: z.string().min(1),
  firstName: z.string().min(1),
  middleName: z.string().optional().default(""),
  lastName: z.string().min(1),
  email: z.string().email(),
  employmentType: z.number(),
  staffType: z.number(),
  contractStartDate: z.string().min(1), // ISO string
  contractEndDate: z.string().min(1),   // ISO string
  licenseNumber: z.string().optional().default(""),
  departmentId: z.string().min(1),
  workLocation: z.string().optional().default(""),
  specializationIds: z.array(z.string()),
  yearsOfExperience: z.number().min(0),
  previousEmployer: z.string().optional().default(""),
  phoneNumber: z.string().optional().default(""),
  emergencyContact: z.string().optional().default(""),
  emergencyPhone: z.string().optional().default(""),
  notes: z.string().optional().default(""),
});

export type Staff = z.infer<typeof StaffSchema>;

// Department schema matching API response
const DepartmentResponseSchema = z.object({
  id: z.string(),
  name: z.string(),
  description: z.string().optional().nullable(),
  departmentCode: z.string(),
  location: z.string().optional().nullable(),
  isActive: z.boolean(),
  createdDate: z.string().optional().nullable(),
  updatedDate: z.string().optional().nullable(),
  recordStatus: z.number().optional().nullable(),
  createdBy: z.string().optional().nullable(),
  updatedBy: z.string().optional().nullable(),
});

// Specialization schema matching API response
const SpecializationResponseSchema = z.object({
  id: z.string(),
  name: z.string(),
  description: z.string().optional().nullable(),
  isPrimary: z.boolean().optional(),
  certificationDate: z.string().optional().nullable(),
  certificationNumber: z.string().optional().nullable(),
});

// Qualification schema (if needed)
const QualificationSchema = z.object({
  id: z.string().optional(),
  name: z.string().optional(),
  institution: z.string().optional().nullable(),
  year: z.number().optional().nullable(),
  // Add other qualification fields as needed
}).passthrough();

// License schema (if needed)
const LicenseSchema = z.object({
  id: z.string().optional(),
  licenseNumber: z.string().optional().nullable(),
  issuingAuthority: z.string().optional().nullable(),
  issueDate: z.string().optional().nullable(),
  expiryDate: z.string().optional().nullable(),
  // Add other license fields as needed
}).passthrough();

// Staff response schema matching the API response structure
export const StaffResponseSchema = z.object({
  id: z.string(),
  userId: z.string().optional().nullable(), // Linked user account id (for password management)
  employeeId: z.string(), // API field name: employeeId (not staffId)
  firstName: z.string(),
  middleName: z.string().optional().nullable(),
  lastName: z.string(),
  email: z.string(),
  dateOfBirth: z.string().optional().nullable(),
  gender: z.string().optional().nullable(),
  address: z.string().optional().nullable(),
  city: z.string().optional().nullable(),
  country: z.string().optional().nullable(),
  emergencyContactRelationship: z.string().optional().nullable(),
  profession: z.string().optional().nullable(),
  qualifications: z.array(QualificationSchema).optional().default([]),
  licenses: z.array(LicenseSchema).optional().default([]),
  employmentType: z.number(),
  contractStartDate: z.string().optional().nullable(),
  contractEndDate: z.string().optional().nullable(),
  hireDate: z.string().optional().nullable(),
  position: z.string().optional().nullable(),
  staffType: z.number(),
  departmentId: z.string(),
  department: DepartmentResponseSchema.optional().nullable(),
  workLocation: z.string().optional().nullable(),
  specializationIds: z.array(z.string()).optional().default([]),
  specializations: z.array(SpecializationResponseSchema).optional().default([]),
  yearsOfExperience: z.number().optional().nullable(),
  phoneNumber: z.string().optional().nullable(),
  emergencyContact: z.string().optional().nullable(),
  emergencyPhone: z.string().optional().nullable(),
  isActive: z.boolean(),
  notes: z.string().optional().nullable().default(""),
  profileImageUrl: z.string().optional().nullable(),
  createdAt: z.string().optional().nullable(),
  updatedAt: z.string().optional().nullable(),
  recordStatus: z.number().optional().nullable(),
  createdBy: z.string().optional().nullable(),
  updatedBy: z.string().optional().nullable(),
  recordStatusChangedAt: z.string().optional().nullable(),
  recordStatusChangedBy: z.string().optional().nullable(),
});

export type StaffResponse = z.infer<typeof StaffResponseSchema>;
export type DepartmentResponse = z.infer<typeof DepartmentResponseSchema>;
export type SpecializationResponse = z.infer<typeof SpecializationResponseSchema>;

/**
 * Maps StaffResponse from API to User format for compatibility
 * Maps employeeId -> staffId
 */
export function mapStaffResponseToUser(staff: StaffResponse): any {
  return {
    ...staff,
    staffId: staff.employeeId, // Map employeeId to staffId
  };
}

/**
 * Maps array of StaffResponse to User array
 */
export function mapStaffResponseArrayToUsers(staffList: StaffResponse[]): any[] {
  return staffList.map(mapStaffResponseToUser);
}


