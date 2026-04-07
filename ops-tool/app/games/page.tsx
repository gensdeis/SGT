'use client';
import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { apiGet, apiPost, hasToken, ApiError } from '@/lib/api';
import Nav from '@/components/Nav';

interface Game {
  id: string; title: string; creator_id: string; time_limit_sec: number;
  tags: string[]; bundle_url: string; bundle_version: string; bundle_hash: string;
}

// 서버는 PUT 사용 — fetch 직접 호출
async function putGame(id: string, body: Partial<Game>) {
  const t = typeof window !== 'undefined' ? window.localStorage.getItem('admin_token') : null;
  const base = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:18081/v1';
  const r = await fetch(`${base}/admin/games/${id}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json', ...(t ? { Authorization: `Bearer ${t}` } : {}) },
    body: JSON.stringify(body),
  });
  if (!r.ok) throw new ApiError(r.status, await r.text());
  return r.json();
}

export default function GamesPage() {
  const router = useRouter();
  const [games, setGames] = useState<Game[]>([]);
  const [err, setErr] = useState('');
  const [editing, setEditing] = useState<Game | null>(null);

  useEffect(() => {
    if (!hasToken()) { router.push('/login'); return; }
    void load();
  }, [router]);

  async function load() {
    try {
      const data = await apiGet<{ games: Game[] }>('/admin/games');
      setGames(data.games || []);
      setErr('');
    } catch (e) {
      setErr(e instanceof ApiError ? e.message : String(e));
    }
  }

  async function save() {
    if (!editing) return;
    try {
      await putGame(editing.id, editing);
      setEditing(null);
      await load();
    } catch (e) {
      setErr(e instanceof ApiError ? e.message : String(e));
    }
  }

  return (
    <main className="p-8 max-w-6xl mx-auto">
      <Nav />
      <h1 className="text-2xl font-bold mb-4">게임 카탈로그</h1>
      {err && <p className="text-red-400 mb-3">{err}</p>}
      <table className="w-full text-sm">
        <thead className="text-left text-zinc-400">
          <tr>
            <th className="py-2 border-b border-zinc-800">ID</th>
            <th className="py-2 border-b border-zinc-800">Title</th>
            <th className="py-2 border-b border-zinc-800">Time</th>
            <th className="py-2 border-b border-zinc-800">Bundle URL</th>
            <th className="py-2 border-b border-zinc-800">Hash</th>
            <th className="py-2 border-b border-zinc-800"></th>
          </tr>
        </thead>
        <tbody>
          {games.map(g => (
            <tr key={g.id} className="hover:bg-zinc-900">
              <td className="py-1 font-mono text-xs">{g.id}</td>
              <td className="py-1">{g.title}</td>
              <td className="py-1">{g.time_limit_sec}s</td>
              <td className="py-1 font-mono text-xs truncate max-w-xs">{g.bundle_url || '—'}</td>
              <td className="py-1 font-mono text-xs">{g.bundle_hash ? g.bundle_hash.slice(0, 12) : '—'}</td>
              <td className="py-1 text-right">
                <button className="px-2 py-1 bg-zinc-800 rounded text-xs hover:bg-zinc-700" onClick={() => setEditing({ ...g })}>
                  편집
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      {editing && (
        <div className="fixed inset-0 bg-black/70 flex items-center justify-center p-4">
          <div className="bg-zinc-900 border border-zinc-700 rounded-lg p-6 w-full max-w-lg space-y-3">
            <h2 className="text-xl font-bold">{editing.id} 편집</h2>
            <Field label="Title" value={editing.title} onChange={v => setEditing({ ...editing, title: v })} />
            <Field label="Bundle URL" value={editing.bundle_url} onChange={v => setEditing({ ...editing, bundle_url: v })} />
            <Field label="Bundle Hash (sha256 hex)" value={editing.bundle_hash} onChange={v => setEditing({ ...editing, bundle_hash: v })} />
            <Field label="Bundle Version" value={editing.bundle_version} onChange={v => setEditing({ ...editing, bundle_version: v })} />
            <div className="flex gap-2 pt-3">
              <button className="flex-1 py-2 bg-orange-600 rounded hover:bg-orange-500" onClick={save}>저장</button>
              <button className="flex-1 py-2 bg-zinc-800 rounded hover:bg-zinc-700" onClick={() => setEditing(null)}>취소</button>
            </div>
          </div>
        </div>
      )}
    </main>
  );
}

function Field({ label, value, onChange }: { label: string; value: string; onChange: (v: string) => void }) {
  return (
    <label className="block">
      <span className="text-xs text-zinc-400">{label}</span>
      <input
        className="mt-1 w-full px-3 py-2 bg-zinc-950 border border-zinc-700 rounded text-sm"
        value={value || ''} onChange={e => onChange(e.target.value)}
      />
    </label>
  );
}
