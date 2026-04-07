'use client';
import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { apiGet, hasToken, ApiError } from '@/lib/api';
import Nav from '@/components/Nav';

interface Game { id: string; title: string }
interface Row { user_id: string; best_score: number; rank: number }

export default function RankingsPage() {
  const router = useRouter();
  const [games, setGames] = useState<Game[]>([]);
  const [gameId, setGameId] = useState('');
  const [rows, setRows] = useState<Row[]>([]);
  const [err, setErr] = useState('');

  useEffect(() => {
    if (!hasToken()) { router.push('/login'); return; }
    apiGet<{ games: Game[] }>('/admin/games').then(d => {
      setGames(d.games || []);
      if (d.games && d.games.length > 0) setGameId(d.games[0].id);
    }).catch(e => setErr(e instanceof ApiError ? e.message : String(e)));
  }, [router]);

  useEffect(() => {
    if (!gameId) return;
    apiGet<{ rankings: Row[] }>(`/admin/rankings/${gameId}`).then(d => setRows(d.rankings || []))
      .catch(e => setErr(e instanceof ApiError ? e.message : String(e)));
  }, [gameId]);

  return (
    <main className="p-8 max-w-4xl mx-auto">
      <Nav />
      <h1 className="text-2xl font-bold mb-4">랭킹</h1>
      <select
        value={gameId} onChange={e => setGameId(e.target.value)}
        className="mb-4 px-3 py-2 bg-zinc-900 border border-zinc-700 rounded"
      >
        {games.map(g => <option key={g.id} value={g.id}>{g.title} ({g.id})</option>)}
      </select>
      {err && <p className="text-red-400 mb-3">{err}</p>}
      <table className="w-full text-sm">
        <thead className="text-left text-zinc-400">
          <tr>
            <th className="py-2 border-b border-zinc-800 w-12">#</th>
            <th className="py-2 border-b border-zinc-800">UUID</th>
            <th className="py-2 border-b border-zinc-800 text-right">Best Score</th>
          </tr>
        </thead>
        <tbody>
          {rows.map(r => (
            <tr key={r.user_id} className="hover:bg-zinc-900">
              <td className="py-1">{r.rank}</td>
              <td className="py-1 font-mono text-xs">{r.user_id.slice(0, 8)}…</td>
              <td className="py-1 text-right">{r.best_score}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </main>
  );
}
