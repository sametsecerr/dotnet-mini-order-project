import { ApiError } from './api';

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
