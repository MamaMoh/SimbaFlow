import { z } from "zod";

export const UserSchema = z.object({
  id: z.string().optional(),
  userId: z.string().optional(),
  staffId: z.string().min(1, "Staff ID is required"),
  firstName: z.string().min(1, "First name is required"),
  middleName: z.string().optional(),
  lastName: z.string().min(1, "Last name is required"),
  fullName: z.string().optional(),
  username: z.string().optional().nullable(),
  email: z.string().email("Invalid email address"),
  employmentType: z.number().min(1, "Employment type is required"),
  staffType: z.number().min(1, "Staff type is required"),
  contractStartDate: z.string().min(1, "Contract start date is required"),
  contractEndDate: z.string().min(1, "Contract end date is required"),
  licenseNumber: z.string().optional(),
  departmentId: z.string().min(1, "Department is required"),
  workLocation: z.string().optional(),
  specializationIds: z.array(z.string()).optional(),
  yearsOfExperience: z.number().min(0, "Years of experience must be 0 or greater"),
  previousEmployer: z.string().optional(),
  phoneNumber: z.string()
    .optional()
    .refine(
      (val) => !val || /^\+[1-9]\d{1,14}$/.test(val),
      { message: "Phone number must be in international format (e.g., +1234567890)" }
    ),
  emergencyContact: z.string().optional(),
  emergencyPhone: z.string()
    .optional()
    .refine(
      (val) => !val || /^\+[1-9]\d{1,14}$/.test(val),
      { message: "Emergency phone must be in international format (e.g., +1234567890)" }
    ),
  notes: z.string().optional()
});

export const UserUpdateSchema = z.object({
  id: z.string(),
  username: z.string().min(1, "Username is required"),
  email: z.string().email("Invalid email address"),
  fullName: z.string().min(1, "Full name is required"),
  isActive: z.boolean(),
});

export const DepartmentSchema = z.object({
  id: z.string(),
  name: z.string(),
  description: z.string().optional(),
  departmentCode: z.string(),
  location: z.string().optional(),
  isActive: z.boolean(),
});

export const SpecializationSchema = z.object({
  id: z.string(),
  name: z.string(),
  description: z.string().optional(),
});

export type User = z.infer<typeof UserSchema>;
export type UserUpdate = z.infer<typeof UserUpdateSchema>;
export type Department = z.infer<typeof DepartmentSchema>;
export type Specialization = z.infer<typeof SpecializationSchema>;

export const EmploymentType = {
  1: "Full-time",
  2: "Part-time",
  3: "Contract",
  4: "Intern",
} as const;

export const StaffType = {
  1: "Doctor",
  2: "Nurse",
  3: "Technician",
  4: "Administrative",
  5: "Support Staff",
} as const;
