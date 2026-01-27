import * as API from '@/api-client/client/sdk.gen';

import { client } from '@/api-client/client/client.gen';

client.setConfig({
    baseUrl: import.meta.env.VITE_BACKEND_API_URL,
});

export default API;