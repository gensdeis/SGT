// 서버 /v1/admin/* fetch wrapper.
// 토큰은 localStorage 에 저장 (Iter 4a 단순화 — 후속 httpOnly cookie 전환).

const BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:18081/v1';

export class ApiError extends Error {
  status: number;
  body: string;
  constructor(status: number, body: string) {
    super(`API ${status}: ${body}`);
    this.status = status;
    this.body = body;
  }
}

function authHeaders(): Record<string, string> {
  if (typeof window === 'undefined') return {};
  const t = window.localStorage.getItem('admin_token');
  return t ? { Authorization: `Bearer ${t}` } : {};
}

export async function apiGet<T>(path: string): Promise<T> {
  const r = await fetch(BASE + path, { headers: authHeaders() });
  if (!r.ok) throw new ApiError(r.status, await r.text());
  return r.json();
}

export async function apiPost<T>(path: string, body: unknown): Promise<T> {
  const r = await fetch(BASE + path, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', ...authHeaders() },
    body: JSON.stringify(body),
  });
  if (!r.ok) throw new ApiError(r.status, await r.text());
  return r.json();
}

export function setToken(t: string) {
  if (typeof window !== 'undefined') window.localStorage.setItem('admin_token', t);
}

export function clearToken() {
  if (typeof window !== 'undefined') window.localStorage.removeItem('admin_token');
}

export function hasToken(): boolean {
  if (typeof window === 'undefined') return false;
  return !!window.localStorage.getItem('admin_token');
}
