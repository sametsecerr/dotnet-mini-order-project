import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api, toError, type OrderSummary } from '../api';
import { ErrorMessage } from '../ErrorMessage';
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

  if (isLoading) return <p className="state state--loading">Siparisler yukleniyor...</p>;
  if (error) return <ErrorMessage error={error} />;
  if (orders.length === 0) return <p className="state">Henuz siparis olusturulmadi.</p>;

  return (
    <section>
      <header className="page-header">
        <h1>Siparisler</h1>
      </header>

      <table>
        <thead>
          <tr>
            <th>No</th>
            <th>Musteri</th>
            <th>Tarih</th>
            <th>Fiyatlandirma</th>
            <th className="numeric">Urun adedi</th>
            <th className="numeric">Toplam</th>
            <th />
          </tr>
        </thead>
        <tbody>
          {orders.map((order) => (
            <tr key={order.id}>
              <td>#{order.id}</td>
              <td>{order.customerName}</td>
              <td>{formatDateTime(order.createdAtUtc)}</td>
              <td>{order.pricingType}</td>
              <td className="numeric">{order.itemCount}</td>
              <td className="numeric">{formatCurrency(order.totalAmount)}</td>
              <td>
                <Link to={`/orders/${order.id}`}>Detay</Link>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </section>
  );
}
