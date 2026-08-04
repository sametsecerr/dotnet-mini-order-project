import { useCallback, useEffect, useState } from 'react';
import { api, toError } from '../api/client';
import type { Product } from '../api/types';

/**
 * Ürünleri (opsiyonel arama terimiyle) getirir.
 * Arama kutusu her tuşta istek atmasın diye 300 ms debounce uygulanır ve
 * eski istekler AbortController ile iptal edilir.
 */
export function useProducts(search: string) {
  const [products, setProducts] = useState<Product[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<Error | null>(null);
  const [reloadToken, setReloadToken] = useState(0);

  const reload = useCallback(() => setReloadToken((token) => token + 1), []);

  useEffect(() => {
    const controller = new AbortController();
    const timer = setTimeout(async () => {
      setIsLoading(true);
      setError(null);
      try {
        setProducts(await api.getProducts(search, controller.signal));
      } catch (err) {
        if (!controller.signal.aborted) {
          setError(toError(err));
        }
      } finally {
        if (!controller.signal.aborted) {
          setIsLoading(false);
        }
      }
    }, 300);

    return () => {
      clearTimeout(timer);
      controller.abort();
    };
  }, [search, reloadToken]);

  return { products, isLoading, error, reload };
}
