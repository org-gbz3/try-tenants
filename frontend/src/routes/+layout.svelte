<script lang="ts">
	import '../app.css';
	import favicon from '#lib/assets/favicon.svg';
	import { resolve } from '$app/paths';
	import { auth } from '#lib/auth.svelte.ts';

	let { children } = $props();
</script>

<svelte:head>
	<link rel="icon" href={favicon} />
	<title>try-tenants</title>
</svelte:head>

<header class="header">
	<div class="header-inner">
		<a class="brand" href={resolve('/')}>try-tenants</a>
		<nav class="row">
			{#if auth.me}
				{#if auth.isSystemAdmin}
					<a href={resolve('/admin/users')}>管理</a>
				{/if}
				<a href={resolve('/account')}>アカウント</a>
			{:else}
				<a href={resolve('/login')}>サインイン</a>
				<a href={resolve('/register')}>新規登録</a>
			{/if}
		</nav>
	</div>
</header>

<main class="main">
	{@render children()}
</main>

<style>
	.header {
		border-bottom: 1px solid var(--color-border);
		background: var(--color-surface);
	}

	.header-inner,
	.main {
		max-width: 48rem;
		margin: 0 auto;
		padding: var(--space-xl) var(--space-2xl);
	}

	.header-inner {
		display: flex;
		align-items: center;
		justify-content: space-between;
		gap: var(--space-xl);
	}

	.main {
		padding-top: var(--space-3xl);
	}

	.brand {
		color: var(--color-text);
		font-weight: 700;
		text-decoration: none;
	}

	nav {
		gap: var(--space-xl);
	}
</style>
