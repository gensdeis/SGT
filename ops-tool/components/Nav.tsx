'use client';
import Link from 'next/link';
import { usePathname, useRouter } from 'next/navigation';
import { clearToken } from '@/lib/api';

const items = [
  { href: '/', label: '대시보드' },
  { href: '/users', label: '유저' },
  { href: '/games', label: '게임' },
  { href: '/rankings', label: '랭킹' },
  { href: '/sessions', label: '세션' },
  { href: '/notices', label: '공지' },
];

export default function Nav() {
  const path = usePathname();
  const router = useRouter();
  return (
    <nav className="flex gap-1 mb-6 border-b border-zinc-800 pb-3 items-center">
      {items.map(it => {
        const active = path === it.href || (it.href !== '/' && path.startsWith(it.href));
        return (
          <Link
            key={it.href}
            href={it.href}
            className={`px-3 py-1.5 rounded text-sm ${active ? 'bg-orange-600 text-white' : 'text-zinc-400 hover:bg-zinc-800'}`}
          >
            {it.label}
          </Link>
        );
      })}
      <button
        className="ml-auto px-3 py-1.5 rounded text-sm bg-zinc-800 hover:bg-zinc-700"
        onClick={() => { clearToken(); router.push('/login'); }}
      >
        로그아웃
      </button>
    </nav>
  );
}
