"use client";

import { useEffect } from "react";
import { toast } from "sonner";
import { useSignalR } from "@/lib/signalr/signalr-provider";

interface CandidateUpdatedMessage {
  candidateId: string;
  changeType: string;
  field?: string;
  oldValue?: string;
  newValue?: string;
  changedBy: string;
  timestamp: string;
}

interface PersonalNotificationMessage {
  title: string;
  body: string;
  actionUrl?: string;
  severity: "info" | "warning" | "error" | "success";
}

/**
 * Listens to SignalR events and displays toast notifications.
 * Mount this component once in the main layout.
 */
export function NotificationListener() {
  const { subscribe, unsubscribe, status } = useSignalR();

  useEffect(() => {
    if (status !== "connected") return;

    const handleCandidateUpdated = (message: CandidateUpdatedMessage) => {
      toast.info(`Candidate updated: ${message.changeType}`, {
        description: message.field
          ? `${message.field}: ${message.oldValue} → ${message.newValue}`
          : `Changed by ${message.changedBy}`,
        duration: 5000,
      });
    };

    const handleNotification = (message: PersonalNotificationMessage) => {
      const toastFn =
        message.severity === "error"
          ? toast.error
          : message.severity === "warning"
            ? toast.warning
            : message.severity === "success"
              ? toast.success
              : toast.info;

      toastFn(message.title, {
        description: message.body,
        duration: 5000,
        action: message.actionUrl
          ? {
              label: "View",
              onClick: () => {
                window.location.href = message.actionUrl!;
              },
            }
          : undefined,
      });
    };

    subscribe("candidateUpdated", handleCandidateUpdated as any);
    subscribe("notification", handleNotification as any);

    return () => {
      unsubscribe("candidateUpdated", handleCandidateUpdated as any);
      unsubscribe("notification", handleNotification as any);
    };
  }, [status, subscribe, unsubscribe]);

  return null; // This component renders nothing — it only listens
}
