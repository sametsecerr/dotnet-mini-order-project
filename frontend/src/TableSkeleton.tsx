export function TableSkeleton({ columns, rows = 5 }: { columns: number; rows?: number }) {
  return (
    <tbody aria-hidden="true">
      {Array.from({ length: rows }, (_, row) => (
        <tr key={row}>
          {Array.from({ length: columns }, (_, column) => (
            <td key={column}>
              <span
                className={`skeleton ${column === 0 ? '' : 'skeleton--right'}`}
                style={{ width: `${column === 1 ? 70 : 40}%` }}
              />
            </td>
          ))}
        </tr>
      ))}
    </tbody>
  );
}
