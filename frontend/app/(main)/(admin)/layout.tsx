import { redirect } from "next/navigation";
import { getServerSession } from "next-auth";
import { authOptions } from "@/lib/auth/authOption";
import { isMockAuthEnabled } from "@/lib/auth/mock-auth";

export default async function AdminLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  if (isMockAuthEnabled()) {
    return <>{children}</>;
  }

  const session = await getServerSession(authOptions);
  if (!session?.user?.accessToken) redirect("/login");

  return <>{children}</>;
}
