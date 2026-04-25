/** @type {import('next').NextConfig} */
const nextConfig = {
  output: 'standalone',
  reactStrictMode: true,
  // In dev (next dev) we proxy /api → backend so the browser hits one origin.
  // In docker-compose, NGINX handles /api routing; Next never sees those calls.
  async rewrites() {
    const target = process.env.BACKEND_INTERNAL_URL || 'http://localhost:5000';
    return [
      { source: '/api/:path*', destination: `${target}/api/:path*` },
    ];
  },
};
export default nextConfig;
