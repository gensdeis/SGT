'use client';
import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { apiGet, hasToken, clearToken, ApiError } from '@/lib/api';

interface UserRow {
  id: string;
  device_id: string;
  nickname: string;
  coins: number;
  banned: boolean;
}

export default function Home() {
  const router = useRouter();
  const [users, setUsers] = useState<UserRow[]>([]);
  const [q, setQ] = useState('');
  const [err, setErr] = useState('');

  useEffect(() => {
    if (!hasToken()) {
      router.push('/login');
      return;
    }
    void load('');
  }, [router]);

  async function load(query: string) {
    try {
      const data = await apiGet<{ users: UserRow[] }>(`/admin/users?q=${encodeURIComponent(query)}`);
      setUsers(data.users || []);
      setErr('');
    } catch (e) {
      const msg = e instanceof ApiError ? e.message : String(e);
      setErr(msg);
      if (e instanceof ApiError && e.status === 401) {
        clearToken();
        router.push('/login');
      }
    }
  }

  return (
    <main className="p-8 max-w-6xl mx-auto">
      <header className="flex items-center justify-between mb-6">
        <h1 className="text-3xl font-bold">숏게타 운영툴</h1>
        <button
          className="px-3 py-1 bg-zinc-800 rounded hover:bg-zinc-700"
          onClick={() => { clearToken(); router.push('/login'); }}
        >
          로그아웃
        </button>
      </header>

      <section className="mb-6 grid grid-cols-4 gap-4">
        <Card label="총 유저 (검색결과)" value={users.length.toString()} />
        <Card label="밴 유저" value={users.filter(u => u.banned).length.toString()} />
        <Card label="총 코인" value={users.reduce((a, u) => a + u.coins, 0).toString()} />
        <Card label="버전" value="Iter 4a" />
      </section>

      <section>
        <div className="flex gap-2 mb-3">
          <input
            value={q}
            onChange={e => setQ(e.target.value)}
            placeholder="device_id / uuid prefix"
            className="flex-1 px-3 py-2 bg-zinc-900 border border-zinc-700 rounded"
          />
          <button
            className="px-4 py-2 bg-orange-600 rounded hover:bg-orange-500"
            onClick={() => load(q)}
          >
            검색
          </button>
        </div>
        {err && <p className="text-red-400 text-sm mb-2">{err}</p>}
        <table className="w-full text-sm border-collapse">
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
                <td className="py-1 font-mono text-xs">{u.id.slice(0, 8)}…</td>
                <td className="py-1">{u.device_id.slice(0, 12)}</td>
                <td className="py-1">{u.nickname || <span className="text-zinc-500">—</span>}</td>
                <td className="py-1 text-right">{u.coins}</td>
                <td className="py-1">{u.banned ? '🚫' : ''}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>
    </main>
  );
}

function Card({ label, value }: { label: string; value: string }) {
  return (
    <div className="bg-zinc-900 border border-zinc-800 rounded-lg p-4">
      <div className="text-zinc-400 text-xs">{label}</div>
      <div className="text-2xl font-bold mt-1">{value}</div>
    </div>
  );
}
