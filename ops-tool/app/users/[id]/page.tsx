'use client';
import { useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { apiGet, apiPost, hasToken, ApiError } from '@/lib/api';
import Nav from '@/components/Nav';

interface Profile {
  user_id: string;
  nickname: string;
  avatar_id: number;
  coins: number;
}

export default function UserDetailPage() {
  const router = useRouter();
  const params = useParams<{ id: string }>();
  const id = params.id;
  const [p, setP] = useState<Profile | null>(null);
  const [err, setErr] = useState('');
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (!hasToken()) { router.push('/login'); return; }
    void load();
  }, [id]); // eslint-disable-line

  async function load() {
    try {
      const data = await apiGet<Profile>(`/admin/users/${id}`);
      setP(data); setErr('');
    } catch (e) {
      setErr(e instanceof ApiError ? e.message : String(e));
    }
  }

  async function adjust(delta: number) {
    setBusy(true);
    try {
      const res = await apiPost<{ coins: number }>(`/admin/users/${id}/coins`, { delta });
      if (p) setP({ ...p, coins: res.coins });
    } catch (e) {
      setErr(e instanceof ApiError ? e.message : String(e));
    } finally { setBusy(false); }
  }

  return (
    <main className="p-8 max-w-3xl mx-auto">
      <Nav />
      <h1 className="text-2xl font-bold mb-4">유저 상세</h1>
      {err && <p className="text-red-400 mb-3">{err}</p>}
      {p && (
        <div className="bg-zinc-900 border border-zinc-800 rounded-lg p-6 space-y-4">
          <Row label="UUID" value={<span className="font-mono text-xs">{p.user_id}</span>} />
          <Row label="Nickname" value={p.nickname || '—'} />
          <Row label="Avatar ID" value={p.avatar_id} />
          <Row label="Coins" value={<span className="text-2xl font-bold">{p.coins}</span>} />
          <div className="flex gap-2 pt-2">
            {[-100, -10, +10, +100, +1000].map(d => (
              <button
                key={d}
                disabled={busy}
                onClick={() => adjust(d)}
                className={`px-3 py-1.5 rounded text-sm disabled:opacity-50 ${
                  d > 0 ? 'bg-green-700 hover:bg-green-600' : 'bg-red-700 hover:bg-red-600'
                }`}
              >
                {d > 0 ? '+' : ''}{d}
              </button>
            ))}
          </div>
        </div>
      )}
    </main>
  );
}

function Row({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="flex justify-between items-center border-b border-zinc-800 pb-2">
      <span className="text-zinc-400 text-sm">{label}</span>
      <span>{value}</span>
    </div>
  );
}
