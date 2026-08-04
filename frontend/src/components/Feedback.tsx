import { ApiError } from '../api/client';

export function Loading({ label = 'Yukleniyor...' }: { label?: string }) {
  return <p className="state state--loading">{label}</p>;
}

export function EmptyState({ label }: { label: string }) {
  return <p className="state">{label}</p>;
}

/** API hatalarını başlık + sebep listesi olarak gösterir. */
export function ErrorMessage({ error }: { error: Error }) {
  const reasons = error instanceof ApiError ? error.reasons : [];

  return (
    <div className="state state--error" role="alert">
      <strong>{error.message}</strong>
      {reasons.length > 0 && (
        <ul>
          {reasons.map((reason) => (
            <li key={reason}>{reason}</li>
          ))}
        </ul>
      )}
    </div>
  );
}
