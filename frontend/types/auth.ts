export interface User {
  id: string;
  employeeId?: string;
  email: string;
  username: string;
  fullName: string;
  firstName?: string;
  lastName?: string;
  department?: string;
  specialization?: string;
  isActive: boolean;
  lastLogin?: Date;
  grantedClaims: string[];
  isFirstLogin?: boolean;
  // Optional for staff with operational context
  workingDays?: number[];
  workStartHour?: number;
  workEndHour?: number;
  slotDuration?: number;
  roles?: MockRoles[];
}
interface MockRoles {
  id: string;
  name: string;
}

// export interface Permission {
//   id: string;
//   name: string;
//   resource: string;
//   action: string;
// }

export interface LoginCredentials {
  username: string;
  password: string;
}

export interface AuthResponse {
  user: User;
  token: string;
  refreshToken: string;
  expiresIn: number;
}

export interface AuthState {
  user: User | null;
  token: string | null;
  isAuthenticated: boolean;
  isLoading: boolean;
}
