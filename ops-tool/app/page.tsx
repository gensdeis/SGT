'use client';
import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { apiGet, hasToken, clearToken, ApiError } from '@/lib/api';
import Nav from '@/components/Nav';

interface Stats {
  dau: number;
  plays_today: number;
  sessions_today: number;
  total_users: number;
}

export default function DashboardPage() {
  const router = useRouter();
  const [s, setS] = useState<Stats | null>(null);
  const [err, setErr] = useState('');

  useEffect(() => {
    if (!hasToken()) { router.push('/login'); return; }
    apiGet<Stats>('/admin/dashboard').then(setS).catch((e) => {
      setErr(e instanceof ApiError ? e.message : String(e));
      if (e instanceof ApiError && e.status === 401) { clearToken(); router.push('/login'); }
    });
  }, [router]);

  return (
    <main className="p-8 max-w-6xl mx-auto">
      <Nav />
      <h1 className="text-3xl font-bold mb-6">대시보드</h1>
      {err && <p className="text-red-400 mb-3">{err}</p>}
      <div className="grid grid-cols-4 gap-4">
        <Card label="DAU (24h)" value={s?.dau ?? '—'} />
        <Card label="플레이 (24h)" value={s?.plays_today ?? '—'} />
        <Card label="세션 (24h)" value={s?.sessions_today ?? '—'} />
        <Card label="총 유저" value={s?.total_users ?? '—'} />
      </div>
    </main>
  );
}

function Card({ label, value }: { label: string; value: number | string }) {
  return (
    <div className="bg-zinc-900 border border-zinc-800 rounded-lg p-4">
      <div className="text-zinc-400 text-xs">{label}</div>
      <div className="text-3xl font-bold mt-1">{value}</div>
    </div>
  );
}
