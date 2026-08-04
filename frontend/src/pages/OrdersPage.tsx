import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api, toError, type OrderSummary } from '../api';
import { ErrorMessage } from '../ErrorMessage';
import { TableSkeleton } from '../TableSkeleton';
import { formatCurrency, formatDateTime } from '../format';

export function OrdersPage() {
  const [orders, setOrders] = useState<OrderSummary[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<Error | null>(null);

  useEffect(() => {
    let isActive = true;

    api
      .getOrders()
      .then((data) => isActive && setOrders(data))
      .catch((err) => isActive && setError(toError(err)))
      .finally(() => isActive && setIsLoading(false));

    return () => {
      isActive = false;
    };
  }, []);

  if (error) return <ErrorMessage error={error} />;

  if (!isLoading && orders.length === 0) {
    return (
      <section>
        <header className="page-header">
          <h1>Siparisler</h1>
        </header>
        <p className="state state--empty">
          Henuz siparis yok. <Link to="/orders/new">Ilk siparisi olusturun.</Link>
        </p>
      </section>
    );
  }

  return (
    <section>
      <header className="page-header">
        <h1>
          Siparisler
          {!isLoading && <span>{orders.length}</span>}
        </h1>
      </header>

      <table>
        <thead>
          <tr>
            <th>No</th>
            <th>Musteri</th>
            <th>Tarih</th>
            <th>Fiyatlandirma</th>
            <th className="numeric">Urun</th>
            <th className="numeric">Toplam</th>
            <th />
          </tr>
        </thead>

        {isLoading ? (
          <TableSkeleton columns={7} />
        ) : (
          <tbody>
            {orders.map((order) => (
              <tr key={order.id}>
                <td><code>#{order.id}</code></td>
                <td>{order.customerName}</td>
                <td>{formatDateTime(order.createdAtUtc)}</td>
                <td>{order.pricingType}</td>
                <td className="numeric">{order.itemCount}</td>
                <td className="numeric">{formatCurrency(order.totalAmount)}</td>
                <td className="numeric">
                  <Link to={`/orders/${order.id}`}>Detay</Link>
                </td>
              </tr>
            ))}
          </tbody>
        )}
      </table>
    </section>
  );
}
