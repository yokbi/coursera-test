import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // C4 — Docker imajı için: Next çalışması için gereken node_modules'ü
  // .next/standalone içine kopyalıyor, imaj bütün ağacı taşımıyor.
  // Yerel geliştirmeyi (`next dev`) etkilemez.
  output: "standalone",
};

export default nextConfig;
