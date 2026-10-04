import axios from 'axios';

const getApiBaseUrl = (): string => {
  const envUrl = (import.meta.env.VITE_API_BASE_URL as string | undefined)?.trim();
  if (envUrl) {
    return envUrl;
  }
  if (import.meta.env.DEV) {
    return 'http://localhost:5071';
  }
  if (typeof window !== 'undefined' && window.location?.origin && !window.location.origin.includes(':5173')) {
    return window.location.origin;
  }
  throw new Error('Missing VITE_API_BASE_URL environment variable in production configuration.');
};

const apiBaseUrl = getApiBaseUrl();


const apiClient = axios.create({
  baseURL: apiBaseUrl,
  headers: { 'Content-Type': 'application/json' },
});

// Attach JWT token from localStorage on every request
apiClient.interceptors.request.use((config) => {
  const token = localStorage.getItem('token');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

// On 401, clear token so AuthContext can redirect to login
apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      localStorage.removeItem('token');
      // Emit a storage event so AuthContext reacts without importing it here
      window.dispatchEvent(new Event('storage'));
    }
    return Promise.reject(error);
  }
);

export default apiClient;
