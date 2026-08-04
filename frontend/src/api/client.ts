import type { CreateOrderRequest, OrderDetail, OrderSummary, Product } from './types';

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5116';

/**
 * API'den dönen ProblemDetails / ValidationProblemDetails gövdesini taşıyan hata.
 * `reasons` alanı kullanıcıya satır bazlı gösterilir (ör. hangi üründe stok yok).
 */
export class ApiError extends Error {
  readonly status: number;
  readonly reasons: string[];

  constructor(message: string, status: number, reasons: string[] = []) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.reasons = reasons;
  }
}

/** catch blogundan gelen `unknown` degeri guvenle Error'a cevirir. */
export const toError = (error: unknown): Error =>
  error instanceof Error ? error : new Error('Beklenmeyen bir hata olustu.');

interface ProblemDetailsBody {
  title?: string;
  detail?: string;
  reasons?: string[];
  errors?: Record<string, string[]>;
}

async function toApiError(response: Response): Promise<ApiError> {
  let body: ProblemDetailsBody | null = null;
  try {
    body = (await response.json()) as ProblemDetailsBody;
  } catch {
    // Gövde JSON değilse aşağıdaki genel mesaja düşeriz.
  }

  // Model validation hatalarını da tek bir listeye indirgiyoruz.
  const validationMessages = body?.errors ? Object.values(body.errors).flat() : [];
  const reasons = body?.reasons ?? validationMessages;
  const message = body?.detail ?? body?.title ?? `Istek basarisiz oldu (HTTP ${response.status}).`;

  return new ApiError(message, response.status, reasons);
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  let response: Response;
  try {
    response = await fetch(`${API_BASE_URL}${path}`, {
      headers: { 'Content-Type': 'application/json' },
      ...init,
    });
  } catch {
    throw new ApiError('Sunucuya ulasilamadi. API calisiyor mu?', 0);
  }

  if (!response.ok) {
    throw await toApiError(response);
  }

  return (await response.json()) as T;
}

export const api = {
  getProducts: (search?: string, signal?: AbortSignal) => {
    const query = search?.trim() ? `?search=${encodeURIComponent(search.trim())}` : '';
    return request<Product[]>(`/api/products${query}`, { signal });
  },

  getProduct: (id: number) => request<Product>(`/api/products/${id}`),

  getOrders: () => request<OrderSummary[]>('/api/orders'),

  getOrder: (id: number) => request<OrderDetail>(`/api/orders/${id}`),

  createOrder: (payload: CreateOrderRequest) =>
    request<OrderDetail>('/api/orders', { method: 'POST', body: JSON.stringify(payload) }),
};
