'use client';
import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { apiPost, setToken, ApiError } from '@/lib/api';

interface LoginResp {
  token: string;
  login: string;
  role: string;
}

export default function LoginPage() {
  const router = useRouter();
  const [login, setLogin] = useState('');
  const [pw, setPw] = useState('');
  const [err, setErr] = useState('');
  const [busy, setBusy] = useState(false);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setErr(''); setBusy(true);
    try {
      const res = await apiPost<LoginResp>('/admin/login', { login, password: pw });
      setToken(res.token);
      router.push('/');
    } catch (e) {
      setErr(e instanceof ApiError ? e.message : String(e));
    } finally {
      setBusy(false);
    }
  }

  return (
    <main className="min-h-screen flex items-center justify-center p-8">
      <form onSubmit={submit} className="w-full max-w-sm bg-zinc-900 border border-zinc-800 rounded-lg p-6">
        <h1 className="text-2xl font-bold mb-1">숏게타 운영툴</h1>
        <p className="text-zinc-400 text-sm mb-6">관리자 로그인</p>

        <label className="block mb-3">
          <span className="text-xs text-zinc-400">아이디</span>
          <input
            className="mt-1 w-full px-3 py-2 bg-zinc-950 border border-zinc-700 rounded"
            value={login} onChange={e => setLogin(e.target.value)} required autoFocus
          />
        </label>
        <label className="block mb-4">
          <span className="text-xs text-zinc-400">비밀번호</span>
          <input
            type="password"
            className="mt-1 w-full px-3 py-2 bg-zinc-950 border border-zinc-700 rounded"
            value={pw} onChange={e => setPw(e.target.value)} required
          />
        </label>

        {err && <p className="text-red-400 text-sm mb-3">{err}</p>}

        <button
          type="submit" disabled={busy}
          className="w-full py-2 bg-orange-600 rounded hover:bg-orange-500 disabled:opacity-50"
        >
          {busy ? '로그인 중...' : '로그인'}
        </button>
      </form>
    </main>
  );
}
