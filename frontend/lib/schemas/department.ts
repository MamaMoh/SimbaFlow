import { z } from "zod";

export const DepartmentSchema = z.object({
  id: z.string().uuid().optional(),
  name: z.string().min(1, "Name is required").max(200),
  code: z.string().min(1, "Code is required").max(50)
    .regex(/^[A-Z0-9-]+$/, "Code must be uppercase alphanumeric with hyphens"),
  description: z.string().max(500).optional().nullable(),
  parentDepartmentId: z.string().uuid().optional().nullable(),
  headUserId: z.string().uuid().optional().nullable(),
  isActive: z.boolean().default(true),
});

export type Department = z.infer<typeof DepartmentSchema>;

export const CreateDepartmentSchema = DepartmentSchema.omit({ id: true });
export type CreateDepartment = z.infer<typeof CreateDepartmentSchema>;

export const UpdateDepartmentSchema = DepartmentSchema.required({ id: true });
export type UpdateDepartment = z.infer<typeof UpdateDepartmentSchema>;

/** Response from GET /api/departments */
export interface DepartmentListItem {
  id: string;
  name: string;
  code: string;
  description?: string | null;
  parentDepartmentId?: string | null;
  parentDepartmentName?: string | null;
  isActive: boolean;
  userCount: number;
}
