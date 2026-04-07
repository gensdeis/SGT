'use client';
import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { apiGet, hasToken, clearToken, ApiError } from '@/lib/api';
import Nav from '@/components/Nav';

interface UserRow {
  id: string; device_id: string; nickname: string; coins: number; banned: boolean;
}

export default function UsersPage() {
  const router = useRouter();
  const [users, setUsers] = useState<UserRow[]>([]);
  const [q, setQ] = useState('');
  const [err, setErr] = useState('');

  useEffect(() => {
    if (!hasToken()) { router.push('/login'); return; }
    void load('');
  }, [router]);

  async function load(query: string) {
    try {
      const data = await apiGet<{ users: UserRow[] }>(`/admin/users?q=${encodeURIComponent(query)}`);
      setUsers(data.users || []);
      setErr('');
    } catch (e) {
      setErr(e instanceof ApiError ? e.message : String(e));
      if (e instanceof ApiError && e.status === 401) { clearToken(); router.push('/login'); }
    }
  }

  return (
    <main className="p-8 max-w-6xl mx-auto">
      <Nav />
      <h1 className="text-2xl font-bold mb-4">유저 검색</h1>
      <div className="flex gap-2 mb-3">
        <input
          value={q} onChange={e => setQ(e.target.value)}
          placeholder="device_id / uuid prefix"
          className="flex-1 px-3 py-2 bg-zinc-900 border border-zinc-700 rounded"
          onKeyDown={e => e.key === 'Enter' && load(q)}
        />
        <button className="px-4 py-2 bg-orange-600 rounded hover:bg-orange-500" onClick={() => load(q)}>
          검색
        </button>
      </div>
      {err && <p className="text-red-400 text-sm mb-2">{err}</p>}
      <table className="w-full text-sm">
        <thead className="text-left text-zinc-400">
          <tr>
            <th className="py-2 border-b border-zinc-800">UUID</th>
            <th className="py-2 border-b border-zinc-800">Device</th>
            <th className="py-2 border-b border-zinc-800">Nickname</th>
            <th className="py-2 border-b border-zinc-800 text-right">Coins</th>
            <th className="py-2 border-b border-zinc-800">Banned</th>
          </tr>
        </thead>
        <tbody>
          {users.map(u => (
            <tr key={u.id} className="hover:bg-zinc-900">
              <td className="py-1 font-mono text-xs">
                <Link href={`/users/${u.id}`} className="text-orange-400 hover:underline">
                  {u.id.slice(0, 8)}…
                </Link>
              </td>
              <td className="py-1">{u.device_id.slice(0, 12)}</td>
              <td className="py-1">{u.nickname || <span className="text-zinc-500">—</span>}</td>
              <td className="py-1 text-right">{u.coins}</td>
              <td className="py-1">{u.banned ? '🚫' : ''}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </main>
  );
}
