import { NextRequest, NextResponse } from "next/server";
import { apiClient } from "@/lib/api/client";

export async function POST(
  _req: NextRequest,
  { params }: { params: Promise<{ id: string; roleId: string }> }
) {
  try {
    const { id, roleId } = await params;
    const data = await apiClient.post(`/users/${id}/roles/${roleId}`);
    return NextResponse.json(data);
  } catch (e: any) {
    return NextResponse.json(
      { error: e?.message || "Failed to assign role" },
      { status: e?.status || 500 }
    );
  }
}

export async function DELETE(
  _req: NextRequest,
  { params }: { params: Promise<{ id: string; roleId: string }> }
) {
  try {
    const { id, roleId } = await params;
    const data = await apiClient.delete(`/users/${id}/roles/${roleId}`);
    return NextResponse.json(data);
  } catch (e: any) {
    return NextResponse.json(
      { error: e?.message || "Failed to remove role" },
      { status: e?.status || 500 }
    );
  }
}


