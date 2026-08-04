import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api, toError } from '../api/client';
import type { OrderSummary } from '../api/types';
import { EmptyState, ErrorMessage, Loading } from '../components/Feedback';
import { formatCurrency, formatDateTime } from '../lib/format';

export function OrdersPage() {
  const [orders, setOrders] = useState<OrderSummary[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<Error | null>(null);

  useEffect(() => {
    let isActive = true;

    (async () => {
      try {
        const data = await api.getOrders();
        if (isActive) {
          setOrders(data);
        }
      } catch (err) {
        if (isActive) {
          setError(toError(err));
        }
      } finally {
        if (isActive) {
          setIsLoading(false);
        }
      }
    })();

    return () => {
      isActive = false;
    };
  }, []);

  if (isLoading) return <Loading label="Siparisler yukleniyor..." />;
  if (error) return <ErrorMessage error={error} />;
  if (orders.length === 0) return <EmptyState label="Henuz siparis olusturulmadi." />;

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
