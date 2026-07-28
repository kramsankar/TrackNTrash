export interface AuditEntry {
  action: string;
  user: string;
  utc: string;
  note?: string | null;
}

export interface ConsoleException {
  id: number;
  type: string;
  severity: "Low" | "Medium" | "High" | "Critical";
  status: "Open" | "Acknowledged" | "Escalated" | "Resolved";
  checkpoint?: string | null;
  orderLineId?: number | null;
  trayId?: number | null;
  tripId?: number | null;
  storeId?: number | null;
  route?: string | null;
  detail: string;
  frameBlobUri?: string | null;
  photoBlobUri?: string | null;
  createdUtc: string;
  ageMinutes: number;
  audit: AuditEntry[];
}

export interface TimelineEvent {
  scanEventId: number;
  eventType: string;
  verdict?: string | null;
  eventUtc: string;
}

export interface ExceptionDetail {
  exception: ConsoleException;
  timeline: TimelineEvent[];
}

export interface Filters {
  checkpoint?: string;
  severity?: string;
  status?: string;
  route?: string;
}
