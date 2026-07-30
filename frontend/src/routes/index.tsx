import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { useEffect } from "react";
import { useAuth } from "@/hooks/use-auth";
import { LoadingState } from "@/components/common/states";

export const Route = createFileRoute("/")({
  component: Index,
});

function Index() {
  const { user, loading } = useAuth();
  const navigate = useNavigate();
  useEffect(() => {
    if (loading) return;
    if (user) navigate({ to: "/app" });
    else navigate({ to: "/login" });
  }, [user, loading, navigate]);
  return (
    <div className="min-h-screen grid place-items-center bg-background">
      <LoadingState label="Betöltés..." />
    </div>
  );
}
