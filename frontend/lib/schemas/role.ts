import { z } from "zod";

export const PermissionSchema = z.object({
  id: z.string(),
  code: z.string(),
  description: z.string(),
  isActive: z.boolean(),
  createdDate: z.string(),
  updatedDate: z.string(),
  recordStatus: z.number(),
  createdBy: z.string(),
  updatedBy: z.string(),
});

export const RoleSchema = z.object({
  id: z.string().optional(),
  name: z.string().min(1, "Name is required"),
  description: z.string().min(1, "Description is required"),
  permissionIds: z.array(z.string()).optional(),
  isActive: z.boolean().default(true),
});

export type Role = z.infer<typeof RoleSchema>;
export type Permission = z.infer<typeof PermissionSchema>;

// For form validation
export const CreateRoleSchema = z.object({
  name: z.string().min(1, "Name is required"),
  description: z.string().min(1, "Description is required"),
  permissionIds: z.array(z.string()).optional(),
});

export const UpdateRoleSchema = z.object({
  id: z.string(),
  name: z.string().min(1, "Name is required"),
  description: z.string().min(1, "Description is required"),
  permissionIds: z.array(z.string()).optional(),
});

export type CreateRole = z.infer<typeof CreateRoleSchema>;
export type UpdateRole = z.infer<typeof UpdateRoleSchema>;
