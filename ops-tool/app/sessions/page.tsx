'use client';
import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { apiGet, hasToken, ApiError } from '@/lib/api';
import Nav from '@/components/Nav';

interface SessionRow {
  id: string;
  user_id: string;
  started_at: string;
  ended_at?: string;
  game_count: number;
  total_score: number;
}

export default function SessionsPage() {
  const router = useRouter();
  const [rows, setRows] = useState<SessionRow[]>([]);
  const [err, setErr] = useState('');

  useEffect(() => {
    if (!hasToken()) { router.push('/login'); return; }
    apiGet<{ sessions: SessionRow[] }>('/admin/sessions').then(d => setRows(d.sessions || []))
      .catch(e => setErr(e instanceof ApiError ? e.message : String(e)));
  }, [router]);

  return (
    <main className="p-8 max-w-6xl mx-auto">
      <Nav />
      <h1 className="text-2xl font-bold mb-4">최근 세션</h1>
      {err && <p className="text-red-400 mb-3">{err}</p>}
      <table className="w-full text-sm">
        <thead className="text-left text-zinc-400">
          <tr>
            <th className="py-2 border-b border-zinc-800">시작</th>
            <th className="py-2 border-b border-zinc-800">User</th>
            <th className="py-2 border-b border-zinc-800 text-right">Games</th>
            <th className="py-2 border-b border-zinc-800 text-right">Total</th>
            <th className="py-2 border-b border-zinc-800">Ended</th>
          </tr>
        </thead>
        <tbody>
          {rows.map(r => (
            <tr key={r.id} className="hover:bg-zinc-900">
              <td className="py-1 text-xs">{new Date(r.started_at).toLocaleString('ko-KR')}</td>
              <td className="py-1 font-mono text-xs">{r.user_id.slice(0, 8)}…</td>
              <td className="py-1 text-right">{r.game_count}</td>
              <td className="py-1 text-right">{r.total_score}</td>
              <td className="py-1 text-xs">{r.ended_at ? '✓' : <span className="text-yellow-500">진행</span>}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </main>
  );
}
