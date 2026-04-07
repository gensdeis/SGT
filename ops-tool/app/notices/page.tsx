'use client';
import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { apiGet, apiPost, hasToken, ApiError } from '@/lib/api';
import Nav from '@/components/Nav';

interface Notice {
  id: number;
  title: string;
  body: string;
  created_at: string;
  expires_at?: string;
}

async function deleteNotice(id: number) {
  const t = typeof window !== 'undefined' ? window.localStorage.getItem('admin_token') : null;
  const base = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:18081/v1';
  const r = await fetch(`${base}/admin/notices/${id}`, {
    method: 'DELETE',
    headers: t ? { Authorization: `Bearer ${t}` } : {},
  });
  if (!r.ok) throw new ApiError(r.status, await r.text());
}

export default function NoticesPage() {
  const router = useRouter();
  const [list, setList] = useState<Notice[]>([]);
  const [title, setTitle] = useState('');
  const [body, setBody] = useState('');
  const [days, setDays] = useState(7);
  const [err, setErr] = useState('');

  useEffect(() => {
    if (!hasToken()) { router.push('/login'); return; }
    void load();
  }, [router]);

  async function load() {
    try {
      const d = await apiGet<{ notices: Notice[] }>('/admin/notices');
      setList(d.notices || []);
      setErr('');
    } catch (e) {
      setErr(e instanceof ApiError ? e.message : String(e));
    }
  }

  async function create() {
    if (!title) { setErr('제목 필수'); return; }
    try {
      await apiPost('/admin/notices', { title, body, expires_days: days });
      setTitle(''); setBody('');
      await load();
    } catch (e) {
      setErr(e instanceof ApiError ? e.message : String(e));
    }
  }

  async function remove(id: number) {
    if (!confirm('삭제?')) return;
    try { await deleteNotice(id); await load(); }
    catch (e) { setErr(e instanceof ApiError ? e.message : String(e)); }
  }

  return (
    <main className="p-8 max-w-4xl mx-auto">
      <Nav />
      <h1 className="text-2xl font-bold mb-4">공지</h1>

      <section className="bg-zinc-900 border border-zinc-800 rounded-lg p-4 mb-6 space-y-3">
        <h2 className="font-bold">새 공지</h2>
        <input
          className="w-full px-3 py-2 bg-zinc-950 border border-zinc-700 rounded text-sm"
          placeholder="제목" value={title} onChange={e => setTitle(e.target.value)}
        />
        <textarea
          className="w-full px-3 py-2 bg-zinc-950 border border-zinc-700 rounded text-sm"
          placeholder="본문" rows={4} value={body} onChange={e => setBody(e.target.value)}
        />
        <div className="flex items-center gap-2">
          <label className="text-xs text-zinc-400">만료 (일):</label>
          <input
            type="number" min={0} max={365} value={days} onChange={e => setDays(parseInt(e.target.value) || 0)}
            className="w-20 px-2 py-1 bg-zinc-950 border border-zinc-700 rounded text-sm"
          />
          <span className="text-xs text-zinc-500">(0 = 무기한)</span>
          <button className="ml-auto px-4 py-1.5 bg-orange-600 rounded hover:bg-orange-500 text-sm" onClick={create}>
            등록
          </button>
        </div>
      </section>

      {err && <p className="text-red-400 mb-3">{err}</p>}

      <ul className="space-y-2">
        {list.map(n => (
          <li key={n.id} className="bg-zinc-900 border border-zinc-800 rounded p-3">
            <div className="flex items-start justify-between">
              <div className="flex-1">
                <div className="font-bold">{n.title}</div>
                <div className="text-sm text-zinc-400 whitespace-pre-wrap mt-1">{n.body}</div>
                <div className="text-xs text-zinc-500 mt-2">
                  {new Date(n.created_at).toLocaleString('ko-KR')}
                  {n.expires_at && ` · 만료 ${new Date(n.expires_at).toLocaleDateString('ko-KR')}`}
                </div>
              </div>
              <button className="ml-3 px-2 py-1 bg-red-700 rounded text-xs hover:bg-red-600" onClick={() => remove(n.id)}>
                삭제
              </button>
            </div>
          </li>
        ))}
      </ul>
    </main>
  );
}
