#!/usr/bin/env node

import { Server } from '@modelcontextprotocol/sdk/server/index.js';
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import {
  CallToolRequestSchema,
  ListToolsRequestSchema,
} from '@modelcontextprotocol/sdk/types.js';

const GITHUB_TOKEN = process.env.GITHUB_TOKEN;
const GIST_ID = process.env.GIST_ID;

if (!GITHUB_TOKEN) {
  console.error('GITHUB_TOKEN environment variable is required');
  process.exit(1);
}

const server = new Server(
  {
    name: 'battleluck-gist-sync',
    version: '1.0.0',
  },
  {
    capabilities: {
      tools: {},
    },
  }
);

server.setRequestHandler(ListToolsRequestSchema, async () => ({
  tools: [
    {
      name: 'gist_read',
      description: 'Read file content from the configured Gist',
      inputSchema: {
        type: 'object',
        properties: {
          filename: {
            type: 'string',
            description: 'Filename to read from the Gist',
          },
        },
        required: ['filename'],
      },
    },
    {
      name: 'gist_list',
      description: 'List all files in the configured Gist',
      inputSchema: {
        type: 'object',
        properties: {},
      },
    },
    {
      name: 'gist_sync',
      description: 'Sync a local file to the Gist',
      inputSchema: {
        type: 'object',
        properties: {
          filename: {
            type: 'string',
            description: 'Filename to sync',
          },
          content: {
            type: 'string',
            description: 'File content to write',
          },
        },
        required: ['filename', 'content'],
      },
    },
  ],
}));

server.setRequestHandler(CallToolRequestSchema, async (request) => {
  const { name, arguments: args } = request.params;

  try {
    switch (name) {
      case 'gist_read':
        return await handleGistRead(args.filename);
      case 'gist_list':
        return await handleGistList();
      case 'gist_sync':
        return await handleGistSync(args.filename, args.content);
      default:
        throw new Error(`Unknown tool: ${name}`);
    }
  } catch (error) {
    return {
      content: [
        {
          type: 'text',
          text: `Error: ${error.message}`,
        },
      ],
      isError: true,
    };
  }
});

async function handleGistRead(filename) {
  if (!GIST_ID) {
    throw new Error('GIST_ID environment variable is required for gist_read');
  }

  const gist = await fetchGist();
  const file = gist.files[filename];
  
  if (!file) {
    throw new Error(`File ${filename} not found in Gist`);
  }

  return {
    content: [
      {
        type: 'text',
        text: file.content || '',
      },
    ],
  };
}

async function handleGistList() {
  if (!GIST_ID) {
    throw new Error('GIST_ID environment variable is required for gist_list');
  }

  const gist = await fetchGist();
  const files = Object.keys(gist.files).map(name => ({
    name,
    size: gist.files[name].size,
    language: gist.files[name].language,
  }));

  return {
    content: [
      {
        type: 'text',
        text: JSON.stringify(files, null, 2),
      },
    ],
  };
}

async function handleGistSync(filename, content) {
  if (!GIST_ID) {
    throw new Error('GIST_ID environment variable is required for gist_sync');
  }

  const gist = await fetchGist();
  
  if (!gist.files[filename]) {
    throw new Error(`File ${filename} does not exist in Gist. Use gist_create to add new files.`);
  }

  gist.files[filename].content = content;

  await updateGist(gist);

  return {
    content: [
      {
        type: 'text',
        text: `Successfully synced ${filename} to Gist`,
      },
    ],
  };
}

async function fetchGist() {
  const response = await fetch(`https://api.github.com/gists/${GIST_ID}`, {
    headers: {
      'Authorization': `token ${GITHUB_TOKEN}`,
      'Accept': 'application/vnd.github.v3+json',
    },
  });

  if (!response.ok) {
    throw new Error(`Failed to fetch Gist: ${response.statusText}`);
  }

  return await response.json();
}

async function updateGist(gist) {
  const response = await fetch(`https://api.github.com/gists/${GIST_ID}`, {
    method: 'PATCH',
    headers: {
      'Authorization': `token ${GITHUB_TOKEN}`,
      'Accept': 'application/vnd.github.v3+json',
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({
      files: gist.files,
    }),
  });

  if (!response.ok) {
    throw new Error(`Failed to update Gist: ${response.statusText}`);
  }

  return await response.json();
}

async function main() {
  const transport = new StdioServerTransport();
  await server.connect(transport);
  console.error('BattleLuck Gist MCP server running on stdio');
}

main().catch((error) => {
  console.error('Fatal error:', error);
  process.exit(1);
});