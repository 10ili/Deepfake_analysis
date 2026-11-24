export interface LoginRequest {
  login: string;
  password: string;
}

export interface LoginResponse {
  token: string;
  expiresAt: string;
  login: string;
  role: 'Admin' | 'User';
}
