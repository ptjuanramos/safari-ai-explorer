import tailwindcss from "@tailwindcss/vite";
import react from "@vitejs/plugin-react";
import { defineConfig } from "vite";
import tsconfigPaths from "vite-tsconfig-paths";

// A static SPA: `npm run build` writes dist/, which the .NET API serves from wwwroot.
// In development the API runs on :5201 (launch profile `http` or `spa`), and /api is proxied to it.
export default defineConfig({
  plugins: [react(), tailwindcss(), tsconfigPaths()],
  server: {
    port: 5173,
    strictPort: true,
    proxy: { "/api": "http://localhost:5201" },
  },
  build: { outDir: "dist" },
});
