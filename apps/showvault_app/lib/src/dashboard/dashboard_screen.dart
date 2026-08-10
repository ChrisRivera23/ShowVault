import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:showvault_app/src/api/showvault_api.dart';
import 'package:showvault_app/src/auth/auth_provider.dart';
import 'package:showvault_app/src/config/app_config.dart';
import 'package:showvault_app/src/recovery/recovery_history_provider.dart';
import 'package:showvault_app/src/recovery/recovery_run.dart';
import 'package:showvault_app/src/scanning/local_catalog_scanner.dart';

class DashboardScreen extends ConsumerWidget {
  const DashboardScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    if (!AppConfig.hasAuth0Client && !AppConfig.personalBetaBypassAuth) {
      return const _ConfigurationRequired();
    }
    return ref
        .watch(authSessionProvider)
        .when(
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (error, _) => _AuthPrompt(error: error),
          data: (session) => session == null
              ? const _AuthPrompt()
              : ref
                    .watch(recoveryHistoryProvider)
                    .when(
                      loading: () =>
                          const Center(child: CircularProgressIndicator()),
                      error: (error, _) => _LoadError(error: error),
                      data: (history) => _LiveDashboard(history: history),
                    ),
        );
  }
}

class _ConfigurationRequired extends StatelessWidget {
  const _ConfigurationRequired();

  @override
  Widget build(BuildContext context) => const _CenteredCard(
    icon: Icons.settings_suggest_outlined,
    title: 'Auth0 client configuration required',
    message:
        'Run with --dart-define=AUTH0_CLIENT_ID=… to connect this native client. See the Flutter app README for the complete command.',
  );
}

class _AuthPrompt extends ConsumerWidget {
  const _AuthPrompt({this.error});
  final Object? error;

  @override
  Widget build(BuildContext context, WidgetRef ref) => _CenteredCard(
    icon: Icons.lock_outline,
    title: 'Sign in to ShowVault',
    message: error == null
        ? 'Use your ShowVault account to connect this computer to cloud backup.'
        : 'Sign-in did not complete. $error',
    action: FilledButton.icon(
      onPressed: () => ref.read(authSessionProvider.notifier).login(),
      icon: const Icon(Icons.login),
      label: const Text('Sign in with Auth0'),
    ),
  );
}

class _LoadError extends ConsumerWidget {
  const _LoadError({required this.error});
  final Object error;

  @override
  Widget build(BuildContext context, WidgetRef ref) => _CenteredCard(
    icon: Icons.cloud_off_outlined,
    title: 'Live history unavailable',
    message: '$error',
    action: FilledButton.icon(
      onPressed: () => ref.invalidate(recoveryHistoryProvider),
      icon: const Icon(Icons.refresh),
      label: const Text('Retry'),
    ),
  );
}

class _LiveDashboard extends ConsumerWidget {
  const _LiveDashboard({required this.history});
  final RecoveryHistory history;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final colors = Theme.of(context).colorScheme;
    return Center(
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 1180),
        child: ListView(
          padding: const EdgeInsets.fromLTRB(24, 24, 24, 48),
          children: [
            Wrap(
              alignment: WrapAlignment.spaceBetween,
              crossAxisAlignment: WrapCrossAlignment.center,
              spacing: 16,
              children: [
                Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'This computer',
                      style: Theme.of(context).textTheme.headlineMedium,
                    ),
                    const SizedBox(height: 6),
                    Text(
                      '${history.organizationName} • ${history.venueName}',
                      style: TextStyle(color: colors.onSurfaceVariant),
                    ),
                  ],
                ),
                const Chip(
                  avatar: Icon(Icons.cloud_done_outlined, size: 18),
                  label: Text('Cloud connected'),
                ),
              ],
            ),
            const SizedBox(height: 24),
            _CandidateOnboarding(history: history),
            if (!AppConfig.personalBetaBypassAuth) ...[
              const SizedBox(height: 24),
              Align(
                alignment: Alignment.centerRight,
                child: TextButton.icon(
                  onPressed: () =>
                      ref.read(authSessionProvider.notifier).logout(),
                  icon: const Icon(Icons.logout),
                  label: const Text('Sign out'),
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

// Legacy Agent network workflow retained outside the simplified customer dashboard.
// ignore: unused_element
class _SubnetOnboarding extends ConsumerWidget {
  const _SubnetOnboarding({required this.history});
  final RecoveryHistory history;

  Future<void> _decide(
    BuildContext context,
    WidgetRef ref,
    SubnetProposal proposal,
    bool approved,
  ) async {
    final session = ref.read(authSessionProvider).valueOrNull;
    if (session == null) return;
    try {
      await ref
          .read(showVaultApiProvider)
          .decideSubnetProposal(
            accessToken: session.accessToken,
            history: history,
            proposalId: proposal.id,
            approved: approved,
          );
      ref.invalidate(recoveryHistoryProvider);
    } catch (error) {
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Subnet decision failed: $error')),
        );
      }
    }
  }

  Future<void> _discover(
    BuildContext context,
    WidgetRef ref,
    SubnetProposal proposal,
  ) async {
    final session = ref.read(authSessionProvider).valueOrNull;
    if (session == null) return;
    try {
      await ref
          .read(showVaultApiProvider)
          .discoverSubnet(
            accessToken: session.accessToken,
            history: history,
            proposalId: proposal.id,
          );
      ref.invalidate(recoveryHistoryProvider);
    } catch (error) {
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Subnet discovery failed: $error')),
        );
      }
    }
  }

  Future<void> _identifyMaLighting(
    BuildContext context,
    WidgetRef ref,
    SubnetProposal proposal,
  ) async {
    final session = ref.read(authSessionProvider).valueOrNull;
    if (session == null) return;
    try {
      await ref
          .read(showVaultApiProvider)
          .identifyMaLighting(
            accessToken: session.accessToken,
            history: history,
            proposalId: proposal.id,
          );
      ref.invalidate(recoveryHistoryProvider);
    } catch (error) {
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('MA Lighting identification failed: $error')),
        );
      }
    }
  }

  Future<void> _identifyYamahaDme(
    BuildContext context,
    WidgetRef ref,
    SubnetProposal proposal,
  ) async {
    final session = ref.read(authSessionProvider).valueOrNull;
    if (session == null) return;
    try {
      await ref
          .read(showVaultApiProvider)
          .identifyYamahaDme(
            accessToken: session.accessToken,
            history: history,
            proposalId: proposal.id,
          );
      ref.invalidate(recoveryHistoryProvider);
    } catch (error) {
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Yamaha DME7 identification failed: $error')),
        );
      }
    }
  }

  Future<void> _identifyGrandMa2(
    BuildContext context,
    WidgetRef ref,
    SubnetProposal proposal,
  ) async {
    final session = ref.read(authSessionProvider).valueOrNull;
    if (session == null) return;
    try {
      await ref
          .read(showVaultApiProvider)
          .identifyGrandMa2(
            accessToken: session.accessToken,
            history: history,
            proposalId: proposal.id,
          );
      ref.invalidate(recoveryHistoryProvider);
    } catch (error) {
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('grandMA2 identification failed: $error')),
        );
      }
    }
  }

  Future<void> _identifyBlackmagicVideohub(
    BuildContext context,
    WidgetRef ref,
    SubnetProposal proposal,
  ) async {
    final session = ref.read(authSessionProvider).valueOrNull;
    if (session == null) return;
    try {
      await ref
          .read(showVaultApiProvider)
          .identifyBlackmagicVideohub(
            accessToken: session.accessToken,
            history: history,
            proposalId: proposal.id,
          );
      ref.invalidate(recoveryHistoryProvider);
    } catch (error) {
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('Blackmagic Videohub identification failed: $error'),
          ),
        );
      }
    }
  }

  Future<void> _identifyNewTekTriCaster(
    BuildContext context,
    WidgetRef ref,
    SubnetProposal proposal,
  ) async {
    final session = ref.read(authSessionProvider).valueOrNull;
    if (session == null) return;
    try {
      await ref
          .read(showVaultApiProvider)
          .identifyNewTekTriCaster(
            accessToken: session.accessToken,
            history: history,
            proposalId: proposal.id,
          );
      ref.invalidate(recoveryHistoryProvider);
    } catch (error) {
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('NewTek TriCaster identification failed: $error'),
          ),
        );
      }
    }
  }

  Future<void> _identifyBirdDog(
    BuildContext context,
    WidgetRef ref,
    SubnetProposal proposal,
  ) async {
    final session = ref.read(authSessionProvider).valueOrNull;
    if (session == null) return;
    try {
      await ref
          .read(showVaultApiProvider)
          .identifyBirdDog(
            accessToken: session.accessToken,
            history: history,
            proposalId: proposal.id,
          );
      ref.invalidate(recoveryHistoryProvider);
    } catch (error) {
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('BirdDog identification failed: $error')),
        );
      }
    }
  }

  Future<void> _identifyPanasonicCamera(
    BuildContext context,
    WidgetRef ref,
    SubnetProposal proposal,
  ) async {
    final session = ref.read(authSessionProvider).valueOrNull;
    if (session == null) return;
    try {
      await ref
          .read(showVaultApiProvider)
          .identifyPanasonicCamera(
            accessToken: session.accessToken,
            history: history,
            proposalId: proposal.id,
          );
      ref.invalidate(recoveryHistoryProvider);
    } catch (error) {
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('Panasonic camera identification failed: $error'),
          ),
        );
      }
    }
  }

  Future<void> _identifySonyCamera(
    BuildContext context,
    WidgetRef ref,
    SubnetProposal proposal,
  ) async {
    final session = ref.read(authSessionProvider).valueOrNull;
    if (session == null) return;
    try {
      await ref
          .read(showVaultApiProvider)
          .identifySonyCamera(
            accessToken: session.accessToken,
            history: history,
            proposalId: proposal.id,
          );
      ref.invalidate(recoveryHistoryProvider);
    } catch (error) {
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Sony camera identification failed: $error')),
        );
      }
    }
  }

  Future<void> _identifyAllenHeathQu(
    BuildContext context,
    WidgetRef ref,
    SubnetProposal proposal,
  ) async {
    final session = ref.read(authSessionProvider).valueOrNull;
    if (session == null) return;
    try {
      await ref
          .read(showVaultApiProvider)
          .identifyAllenHeathQu(
            accessToken: session.accessToken,
            history: history,
            proposalId: proposal.id,
          );
      ref.invalidate(recoveryHistoryProvider);
    } catch (error) {
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('Allen & Heath Qu identification failed: $error'),
          ),
        );
      }
    }
  }

  Future<void> _identifyBehringerWing(
    BuildContext context,
    WidgetRef ref,
    SubnetProposal proposal,
  ) async {
    final session = ref.read(authSessionProvider).valueOrNull;
    if (session == null) return;
    try {
      await ref
          .read(showVaultApiProvider)
          .identifyBehringerWing(
            accessToken: session.accessToken,
            history: history,
            proposalId: proposal.id,
          );
      ref.invalidate(recoveryHistoryProvider);
    } catch (error) {
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('Behringer WING identification failed: $error'),
          ),
        );
      }
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final visible = history.subnetProposals.where(
      (p) => p.decision != 'rejected',
    );
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'Venue network proposals',
              style: Theme.of(context).textTheme.titleLarge,
            ),
            const SizedBox(height: 6),
            const Text(
              'Review directly connected local subnets. Approval records scope only and does not start a scan.',
            ),
            const SizedBox(height: 16),
            if (visible.isEmpty)
              const Text('No subnets are waiting for review.')
            else
              for (final proposal in visible)
                ListTile(
                  contentPadding: EdgeInsets.zero,
                  leading: const Icon(Icons.lan_outlined),
                  title: Text('${proposal.network}/${proposal.prefixLength}'),
                  subtitle: Text(
                    '${proposal.interfaceType} • ${proposal.agentName}\n'
                    '${proposal.evidence}${_discoveryDetail(proposal)}'
                    '${_identificationDetail(proposal)}${_grandMa2IdentificationDetail(proposal)}'
                    '${_yamahaIdentificationDetail(proposal)}'
                    '${_blackmagicVideohubIdentificationDetail(proposal)}'
                    '${_newTekTriCasterIdentificationDetail(proposal)}'
                    '${_birdDogIdentificationDetail(proposal)}'
                    '${_panasonicCameraIdentificationDetail(proposal)}'
                    '${_sonyCameraIdentificationDetail(proposal)}'
                    '${_allenHeathQuIdentificationDetail(proposal)}'
                    '${_behringerWingIdentificationDetail(proposal)}',
                  ),
                  isThreeLine: true,
                  trailing: proposal.decision == 'approved'
                      ? proposal.discoveryStatus == 'pending'
                            ? const Chip(label: Text('Discovering'))
                            : Wrap(
                                spacing: 8,
                                children: [
                                  OutlinedButton(
                                    onPressed: () =>
                                        _discover(context, ref, proposal),
                                    child: Text(
                                      proposal.discoveryStatus == null
                                          ? 'Authorize discovery'
                                          : 'Discover again',
                                    ),
                                  ),
                                  if (proposal.discoveryStatus == 'completed' &&
                                      (proposal.respondingHostCount ?? 0) > 0)
                                    if (proposal.identificationStatus ==
                                        'pending')
                                      const Chip(
                                        label: Text('Identifying grandMA3'),
                                      )
                                    else
                                      FilledButton(
                                        onPressed: () => _identifyMaLighting(
                                          context,
                                          ref,
                                          proposal,
                                        ),
                                        child: const Text('Identify grandMA3'),
                                      ),
                                  if (proposal.discoveryStatus == 'completed' &&
                                      (proposal.respondingHostCount ?? 0) > 0)
                                    if (proposal.grandMa2IdentificationStatus ==
                                        'pending')
                                      const Chip(
                                        label: Text('Identifying grandMA2'),
                                      )
                                    else
                                      FilledButton(
                                        onPressed: () => _identifyGrandMa2(
                                          context,
                                          ref,
                                          proposal,
                                        ),
                                        child: const Text('Identify grandMA2'),
                                      ),
                                  if (proposal.discoveryStatus == 'completed' &&
                                      (proposal.respondingHostCount ?? 0) > 0)
                                    if (proposal
                                            .blackmagicVideohubIdentificationStatus ==
                                        'pending')
                                      const Chip(
                                        label: Text('Identifying Videohub'),
                                      )
                                    else
                                      FilledButton(
                                        onPressed: () =>
                                            _identifyBlackmagicVideohub(
                                              context,
                                              ref,
                                              proposal,
                                            ),
                                        child: const Text(
                                          'Identify Blackmagic Videohub',
                                        ),
                                      ),
                                  if (proposal.discoveryStatus == 'completed' &&
                                      (proposal.respondingHostCount ?? 0) > 0)
                                    if (proposal
                                            .newTekTriCasterIdentificationStatus ==
                                        'pending')
                                      const Chip(
                                        label: Text('Identifying TriCaster'),
                                      )
                                    else
                                      FilledButton(
                                        onPressed: () =>
                                            _identifyNewTekTriCaster(
                                              context,
                                              ref,
                                              proposal,
                                            ),
                                        child: const Text(
                                          'Identify NewTek TriCaster',
                                        ),
                                      ),
                                  if (proposal.discoveryStatus == 'completed' &&
                                      (proposal.respondingHostCount ?? 0) > 0)
                                    if (proposal.birdDogIdentificationStatus ==
                                        'pending')
                                      const Chip(
                                        label: Text('Identifying BirdDog'),
                                      )
                                    else
                                      FilledButton(
                                        onPressed: () => _identifyBirdDog(
                                          context,
                                          ref,
                                          proposal,
                                        ),
                                        child: const Text('Identify BirdDog'),
                                      ),
                                  if (proposal.discoveryStatus == 'completed' &&
                                      (proposal.respondingHostCount ?? 0) > 0)
                                    if (proposal
                                            .panasonicCameraIdentificationStatus ==
                                        'pending')
                                      const Chip(
                                        label: Text(
                                          'Identifying Panasonic camera',
                                        ),
                                      )
                                    else
                                      FilledButton(
                                        onPressed: () =>
                                            _identifyPanasonicCamera(
                                              context,
                                              ref,
                                              proposal,
                                            ),
                                        child: const Text(
                                          'Identify Panasonic camera',
                                        ),
                                      ),
                                  if (proposal.discoveryStatus == 'completed' &&
                                      (proposal.respondingHostCount ?? 0) > 0)
                                    if (proposal
                                            .sonyCameraIdentificationStatus ==
                                        'pending')
                                      const Chip(
                                        label: Text('Identifying Sony camera'),
                                      )
                                    else
                                      FilledButton(
                                        onPressed: () => _identifySonyCamera(
                                          context,
                                          ref,
                                          proposal,
                                        ),
                                        child: const Text(
                                          'Identify Sony camera',
                                        ),
                                      ),
                                  if (proposal.discoveryStatus == 'completed' &&
                                      (proposal.respondingHostCount ?? 0) > 0)
                                    if (proposal
                                            .allenHeathQuIdentificationStatus ==
                                        'pending')
                                      const Chip(
                                        label: Text(
                                          'Identifying Allen & Heath Qu',
                                        ),
                                      )
                                    else
                                      FilledButton(
                                        onPressed: () => _identifyAllenHeathQu(
                                          context,
                                          ref,
                                          proposal,
                                        ),
                                        child: const Text(
                                          'Identify Allen & Heath Qu',
                                        ),
                                      ),
                                  if (proposal.discoveryStatus == 'completed' &&
                                      (proposal.respondingHostCount ?? 0) > 0)
                                    if (proposal
                                            .behringerWingIdentificationStatus ==
                                        'pending')
                                      const Chip(
                                        label: Text(
                                          'Identifying Behringer WING',
                                        ),
                                      )
                                    else
                                      FilledButton(
                                        onPressed: () => _identifyBehringerWing(
                                          context,
                                          ref,
                                          proposal,
                                        ),
                                        child: const Text(
                                          'Identify Behringer WING',
                                        ),
                                      ),
                                  if (proposal.discoveryStatus == 'completed' &&
                                      (proposal.respondingHostCount ?? 0) > 0)
                                    if (proposal.yamahaIdentificationStatus ==
                                        'pending')
                                      const Chip(
                                        label: Text('Identifying Yamaha DME7'),
                                      )
                                    else
                                      FilledButton(
                                        onPressed: () => _identifyYamahaDme(
                                          context,
                                          ref,
                                          proposal,
                                        ),
                                        child: const Text(
                                          'Identify Yamaha DME7',
                                        ),
                                      ),
                                ],
                              )
                      : Wrap(
                          spacing: 8,
                          children: [
                            OutlinedButton(
                              onPressed: () =>
                                  _decide(context, ref, proposal, false),
                              child: const Text('Reject'),
                            ),
                            FilledButton(
                              onPressed: () =>
                                  _decide(context, ref, proposal, true),
                              child: const Text('Approve'),
                            ),
                          ],
                        ),
                ),
          ],
        ),
      ),
    );
  }

  String _discoveryDetail(
    SubnetProposal proposal,
  ) => switch (proposal.discoveryStatus) {
    'completed' =>
      '\nDiscovery complete • ${proposal.respondingHostCount ?? 0} of '
          '${proposal.attemptedHostCount ?? 0} hosts responded • '
          '${proposal.passiveCandidateCount ?? 0} passive-cache targets + '
          '${proposal.fallbackTargetCount ?? proposal.attemptedHostCount ?? 0} fallback targets • '
          'reachability only'
          '${proposal.shouldSuggestDiscoveryRetry ? '\nNo device responded. Keep the direct Ethernet link connected and retry discovery so passive neighbor announcements can be observed.' : ''}',
    'failed' =>
      '\nDiscovery failed • ${proposal.discoveryMessage ?? 'Unknown error'}',
    _ => '',
  };

  String _identificationDetail(
    SubnetProposal proposal,
  ) => switch (proposal.identificationStatus) {
    'completed' =>
      '\nMA Lighting review • ${proposal.identifiedHostCount ?? 0} of '
          '${proposal.identificationAttemptedHostCount ?? 0} identified • '
          '${proposal.identifiedProductFamilies ?? 'none'} • addresses remain local',
    'failed' =>
      '\nMA Lighting identification failed • '
          '${proposal.identificationMessage ?? 'Unknown error'}',
    _ => '',
  };

  String _yamahaIdentificationDetail(
    SubnetProposal proposal,
  ) => switch (proposal.yamahaIdentificationStatus) {
    'completed' =>
      '\nYamaha review • ${proposal.yamahaIdentifiedHostCount ?? 0} of '
          '${proposal.yamahaIdentificationAttemptedHostCount ?? 0} identified • '
          '${proposal.yamahaIdentifiedProductFamilies ?? 'none'} • addresses remain local',
    'failed' =>
      '\nYamaha DME7 identification failed • '
          '${proposal.yamahaIdentificationMessage ?? 'Unknown error'}',
    _ => '',
  };

  String _grandMa2IdentificationDetail(
    SubnetProposal proposal,
  ) => switch (proposal.grandMa2IdentificationStatus) {
    'completed' =>
      '\ngrandMA2 review • ${proposal.grandMa2IdentifiedHostCount ?? 0} of '
          '${proposal.grandMa2IdentificationAttemptedHostCount ?? 0} identified • '
          '${proposal.grandMa2IdentifiedProductFamilies ?? 'none'} • addresses remain local',
    'failed' =>
      '\ngrandMA2 identification failed • '
          '${proposal.grandMa2IdentificationMessage ?? 'Unknown error'}',
    _ => '',
  };

  String _blackmagicVideohubIdentificationDetail(
    SubnetProposal proposal,
  ) => switch (proposal.blackmagicVideohubIdentificationStatus) {
    'completed' =>
      '\nBlackmagic Videohub review • '
          '${proposal.blackmagicVideohubIdentifiedHostCount ?? 0} of '
          '${proposal.blackmagicVideohubIdentificationAttemptedHostCount ?? 0} identified • '
          '${proposal.blackmagicVideohubIdentifiedProductFamilies ?? 'none'} • addresses remain local',
    'failed' =>
      '\nBlackmagic Videohub identification failed • '
          '${proposal.blackmagicVideohubIdentificationMessage ?? 'Unknown error'}',
    _ => '',
  };

  String _newTekTriCasterIdentificationDetail(
    SubnetProposal proposal,
  ) => switch (proposal.newTekTriCasterIdentificationStatus) {
    'completed' =>
      '\nNewTek TriCaster review • '
          '${proposal.newTekTriCasterIdentifiedHostCount ?? 0} of '
          '${proposal.newTekTriCasterIdentificationAttemptedHostCount ?? 0} identified • '
          '${proposal.newTekTriCasterIdentifiedProductFamilies ?? 'none'} • addresses and raw system data remain local',
    'failed' =>
      '\nNewTek TriCaster identification failed • '
          '${proposal.newTekTriCasterIdentificationMessage ?? 'Unknown error'}',
    _ => '',
  };

  String _birdDogIdentificationDetail(
    SubnetProposal proposal,
  ) => switch (proposal.birdDogIdentificationStatus) {
    'completed' =>
      '\nBirdDog review • '
          '${proposal.birdDogIdentifiedHostCount ?? 0} of '
          '${proposal.birdDogIdentificationAttemptedHostCount ?? 0} identified • '
          '${proposal.birdDogIdentifiedProductFamilies ?? 'none'} • addresses and raw responses remain local',
    'failed' =>
      '\nBirdDog identification failed • '
          '${proposal.birdDogIdentificationMessage ?? 'Unknown error'}',
    _ => '',
  };

  String _panasonicCameraIdentificationDetail(
    SubnetProposal proposal,
  ) => switch (proposal.panasonicCameraIdentificationStatus) {
    'completed' =>
      '\nPanasonic camera review • '
          '${proposal.panasonicCameraIdentifiedHostCount ?? 0} of '
          '${proposal.panasonicCameraIdentificationAttemptedHostCount ?? 0} identified • '
          '${proposal.panasonicCameraIdentifiedProductFamilies ?? 'none'} • addresses and raw responses remain local',
    'failed' =>
      '\nPanasonic camera identification failed • '
          '${proposal.panasonicCameraIdentificationMessage ?? 'Unknown error'}',
    _ => '',
  };

  String _sonyCameraIdentificationDetail(
    SubnetProposal proposal,
  ) => switch (proposal.sonyCameraIdentificationStatus) {
    'completed' =>
      '\nSony camera review • '
          '${proposal.sonyCameraIdentifiedHostCount ?? 0} of '
          '${proposal.sonyCameraIdentificationAttemptedHostCount ?? 0} identified • '
          '${proposal.sonyCameraIdentifiedProductFamilies ?? 'none'} • addresses and raw system responses remain local',
    'failed' =>
      '\nSony camera identification failed • '
          '${proposal.sonyCameraIdentificationMessage ?? 'Unknown error'}',
    _ => '',
  };

  String _allenHeathQuIdentificationDetail(
    SubnetProposal proposal,
  ) => switch (proposal.allenHeathQuIdentificationStatus) {
    'completed' =>
      '\nAllen & Heath Qu review • '
          '${proposal.allenHeathQuIdentifiedHostCount ?? 0} of '
          '${proposal.allenHeathQuIdentificationAttemptedHostCount ?? 0} identified • '
          '${proposal.allenHeathQuIdentifiedProductFamilies ?? 'none'} • addresses and raw MIDI state responses remain local',
    'failed' =>
      '\nAllen & Heath Qu identification failed • '
          '${proposal.allenHeathQuIdentificationMessage ?? 'Unknown error'}',
    _ => '',
  };

  String _behringerWingIdentificationDetail(
    SubnetProposal proposal,
  ) => switch (proposal.behringerWingIdentificationStatus) {
    'completed' =>
      '\nBehringer WING review • '
          '${proposal.behringerWingIdentifiedHostCount ?? 0} of '
          '${proposal.behringerWingIdentificationAttemptedHostCount ?? 0} identified • '
          '${proposal.behringerWingIdentifiedProductFamilies ?? 'none'} • addresses, console names, serials, firmware, and raw replies remain local',
    'failed' =>
      '\nBehringer WING identification failed • '
          '${proposal.behringerWingIdentificationMessage ?? 'Unknown error'}',
    _ => '',
  };
}

class _CandidateOnboarding extends ConsumerWidget {
  const _CandidateOnboarding({required this.history});
  final RecoveryHistory history;

  Future<void> _scanComputer(BuildContext context, WidgetRef ref) async {
    final session = ref.read(authSessionProvider).valueOrNull;
    if (session == null) return;
    try {
      final candidateKeys = await ref.read(localCatalogScannerProvider).scan();
      final count = await ref
          .read(showVaultApiProvider)
          .submitComputerScan(
            accessToken: session.accessToken,
            history: history,
            candidateKeys: candidateKeys,
          );
      if (!context.mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('Computer scan complete • $count candidates found.'),
        ),
      );
      ref.invalidate(recoveryHistoryProvider);
    } catch (error) {
      if (!context.mounted) return;
      ScaffoldMessenger.of(
        context,
      ).showSnackBar(SnackBar(content: Text('Computer scan failed: $error')));
    }
  }

  Future<void> _decide(
    BuildContext context,
    WidgetRef ref,
    RecoveryCandidate candidate,
    bool approved,
  ) async {
    final session = ref.read(authSessionProvider).valueOrNull;
    if (session == null) return;
    try {
      await ref
          .read(showVaultApiProvider)
          .decideRecoveryCandidate(
            accessToken: session.accessToken,
            history: history,
            candidateId: candidate.id,
            approved: approved,
          );
      ref.invalidate(recoveryHistoryProvider);
    } catch (error) {
      if (!context.mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Candidate decision failed: $error')),
      );
    }
  }

  Future<void> _validate(
    BuildContext context,
    WidgetRef ref,
    RecoveryCandidate candidate,
  ) async {
    final session = ref.read(authSessionProvider).valueOrNull;
    if (session == null) return;
    try {
      await ref
          .read(showVaultApiProvider)
          .validateRecoveryCandidate(
            accessToken: session.accessToken,
            history: history,
            candidateId: candidate.id,
          );
      ref.invalidate(recoveryHistoryProvider);
      if (!context.mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Product validation queued.')),
      );
    } catch (error) {
      if (!context.mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Product validation failed: $error')),
      );
    }
  }

  Future<void> _backup(
    BuildContext context,
    WidgetRef ref,
    RecoveryCandidate candidate,
  ) async {
    final session = ref.read(authSessionProvider).valueOrNull;
    if (session == null) return;
    try {
      await ref
          .read(showVaultApiProvider)
          .backupRecoveryCandidate(
            accessToken: session.accessToken,
            history: history,
            candidateId: candidate.id,
          );
      if (!context.mounted) return;
      ScaffoldMessenger.of(
        context,
      ).showSnackBar(const SnackBar(content: Text('Recovery backup queued.')));
    } catch (error) {
      if (!context.mounted) return;
      ScaffoldMessenger.of(
        context,
      ).showSnackBar(SnackBar(content: Text('Recovery backup failed: $error')));
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final visible = history.candidates
        .where((candidate) => candidate.decision != 'rejected')
        .toList(growable: false);
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Wrap(
              alignment: WrapAlignment.spaceBetween,
              crossAxisAlignment: WrapCrossAlignment.center,
              spacing: 16,
              runSpacing: 12,
              children: [
                Text(
                  'Detected systems',
                  style: Theme.of(context).textTheme.titleLarge,
                ),
                FilledButton.icon(
                  onPressed: () => _scanComputer(context, ref),
                  icon: const Icon(Icons.computer_rounded),
                  label: const Text('Scan this computer'),
                ),
              ],
            ),
            const SizedBox(height: 6),
            const Text(
              'Scan only exact catalog-defined locations, then review recognized systems. Unrelated applications are not inventoried, and filesystem paths remain in memory on this computer.',
            ),
            const SizedBox(height: 16),
            if (visible.isEmpty)
              const Text('No systems are waiting for review.')
            else
              for (final candidate in visible)
                ListTile(
                  contentPadding: EdgeInsets.zero,
                  leading: const Icon(Icons.devices_other_outlined),
                  title: Text(candidate.productName),
                  subtitle: Text(
                    '${candidate.candidateType} • ${candidate.agentName}\n'
                    '${candidate.evidence}${_validationDetail(candidate)}',
                  ),
                  isThreeLine: true,
                  trailing: candidate.agentName == 'This computer'
                      ? const Chip(label: Text('Detected'))
                      : candidate.decision == 'approved'
                      ? candidate.candidateType == 'UserDataRoot'
                            ? candidate.validationStatus == 'pending'
                                  ? const Chip(label: Text('Validating'))
                                  : candidate.validationStatus == 'passed'
                                  ? FilledButton.icon(
                                      onPressed: () =>
                                          _backup(context, ref, candidate),
                                      icon: const Icon(
                                        Icons.inventory_2_outlined,
                                      ),
                                      label: const Text('Back up'),
                                    )
                                  : FilledButton.icon(
                                      onPressed: () =>
                                          _validate(context, ref, candidate),
                                      icon: const Icon(
                                        Icons.fact_check_outlined,
                                      ),
                                      label: const Text('Validate'),
                                    )
                            : const Chip(label: Text('Approved'))
                      : Wrap(
                          spacing: 8,
                          children: [
                            OutlinedButton(
                              onPressed: () =>
                                  _decide(context, ref, candidate, false),
                              child: const Text('Reject'),
                            ),
                            FilledButton(
                              onPressed: () =>
                                  _decide(context, ref, candidate, true),
                              child: const Text('Approve'),
                            ),
                          ],
                        ),
                ),
          ],
        ),
      ),
    );
  }

  String _validationDetail(
    RecoveryCandidate candidate,
  ) => switch (candidate.validationStatus) {
    'passed' =>
      '\nValidated • ${candidate.validationFileCount ?? 0} files${candidate.validationTruncated == true ? ' • truncated' : ''}',
    'failed' =>
      '\nValidation failed • ${candidate.validationMessage ?? 'Unknown error'}',
    _ => '',
  };
}

class _RecoveryControls extends ConsumerStatefulWidget {
  const _RecoveryControls({required this.history});
  final RecoveryHistory history;

  @override
  ConsumerState<_RecoveryControls> createState() => _RecoveryControlsState();
}

class _RecoveryControlsState extends ConsumerState<_RecoveryControls> {
  final _pluginController = TextEditingController(text: 'showvault.filesystem');
  final _rootController = TextEditingController();
  final _restoreController = TextEditingController();
  String? _agentId;
  String? _discoveryCommandId;
  String? _backupCommandId;
  String? _verificationCommandId;
  String? _restoreCommandId;
  bool _busy = false;

  @override
  void dispose() {
    _pluginController.dispose();
    _rootController.dispose();
    _restoreController.dispose();
    super.dispose();
  }

  Future<void> _run(
    String successMessage,
    Future<String> Function(ShowVaultApi api, String token, String agentId)
    operation,
    void Function(String commandId) remember,
  ) async {
    final agentId =
        _agentId ??
        (widget.history.agents.length == 1
            ? widget.history.agents.single.id
            : null);
    final session = ref.read(authSessionProvider).valueOrNull;
    if (agentId == null || session == null) return;
    setState(() => _busy = true);
    try {
      final commandId = await operation(
        ref.read(showVaultApiProvider),
        session.accessToken,
        agentId,
      );
      if (!mounted) return;
      setState(() => remember(commandId));
      ref.invalidate(recoveryHistoryProvider);
      ScaffoldMessenger.of(
        context,
      ).showSnackBar(SnackBar(content: Text(successMessage)));
    } catch (error) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Recovery command failed: $error')),
      );
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final history = widget.history;
    final selectedAgent =
        _agentId ??
        (history.agents.length == 1 ? history.agents.single.id : null);
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'Run recovery workflow',
              style: Theme.of(context).textTheme.titleLarge,
            ),
            const SizedBox(height: 6),
            const Text(
              'Use exact paths already allowlisted in the selected Venue Agent.',
            ),
            const SizedBox(height: 18),
            if (history.agents.isEmpty)
              const Text('No active Venue Agent is enrolled.')
            else ...[
              DropdownButtonFormField<String>(
                initialValue: selectedAgent,
                decoration: const InputDecoration(labelText: 'Venue Agent'),
                items: [
                  for (final agent in history.agents)
                    DropdownMenuItem(value: agent.id, child: Text(agent.name)),
                ],
                onChanged: _busy
                    ? null
                    : (value) => setState(() => _agentId = value),
              ),
              const SizedBox(height: 12),
              TextField(
                controller: _pluginController,
                enabled: !_busy && _discoveryCommandId == null,
                onChanged: (_) => setState(() {}),
                decoration: const InputDecoration(labelText: 'Plugin ID'),
              ),
              const SizedBox(height: 12),
              TextField(
                controller: _rootController,
                enabled: !_busy && _discoveryCommandId == null,
                onChanged: (_) => setState(() {}),
                decoration: const InputDecoration(
                  labelText: 'Exact discovery root',
                ),
              ),
              const SizedBox(height: 12),
              TextField(
                controller: _restoreController,
                enabled: !_busy && _verificationCommandId != null,
                onChanged: (_) => setState(() {}),
                decoration: const InputDecoration(
                  labelText: 'Exact restore target',
                ),
              ),
              const SizedBox(height: 18),
              Wrap(
                spacing: 10,
                runSpacing: 10,
                children: [
                  FilledButton.icon(
                    onPressed:
                        _busy ||
                            selectedAgent == null ||
                            _discoveryCommandId != null ||
                            _pluginController.text.trim().isEmpty ||
                            _rootController.text.trim().isEmpty
                        ? null
                        : () => _run(
                            'Discovery queued.',
                            (api, token, agentId) => api.startDiscovery(
                              accessToken: token,
                              history: history,
                              agentId: agentId,
                              pluginId: _pluginController.text.trim(),
                              rootPath: _rootController.text,
                            ),
                            (id) => _discoveryCommandId = id,
                          ),
                    icon: const Icon(Icons.radar_rounded),
                    label: const Text('1. Scan'),
                  ),
                  FilledButton.tonalIcon(
                    onPressed:
                        _busy ||
                            _discoveryCommandId == null ||
                            _backupCommandId != null
                        ? null
                        : () => _run(
                            'Backup queued.',
                            (api, token, agentId) => api.createBackup(
                              accessToken: token,
                              history: history,
                              agentId: agentId,
                              discoveryCommandId: _discoveryCommandId!,
                            ),
                            (id) => _backupCommandId = id,
                          ),
                    icon: const Icon(Icons.inventory_2_rounded),
                    label: const Text('2. Backup'),
                  ),
                  FilledButton.tonalIcon(
                    onPressed:
                        _busy ||
                            _backupCommandId == null ||
                            _verificationCommandId != null
                        ? null
                        : () => _run(
                            'Verification queued.',
                            (api, token, agentId) => api.verifyBackup(
                              accessToken: token,
                              history: history,
                              agentId: agentId,
                              backupCommandId: _backupCommandId!,
                            ),
                            (id) => _verificationCommandId = id,
                          ),
                    icon: const Icon(Icons.verified_rounded),
                    label: const Text('3. Verify'),
                  ),
                  FilledButton.tonalIcon(
                    onPressed:
                        _busy ||
                            _verificationCommandId == null ||
                            _restoreCommandId != null ||
                            _restoreController.text.trim().isEmpty
                        ? null
                        : () => _confirmRestore(history),
                    icon: const Icon(Icons.restore_rounded),
                    label: const Text('4. Restore'),
                  ),
                  IconButton(
                    tooltip: 'Refresh live history',
                    onPressed: _busy
                        ? null
                        : () => ref.invalidate(recoveryHistoryProvider),
                    icon: const Icon(Icons.refresh),
                  ),
                ],
              ),
            ],
          ],
        ),
      ),
    );
  }

  Future<void> _confirmRestore(RecoveryHistory history) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Confirm controlled restore'),
        content: Text(
          'Restore the verified package into ${_restoreController.text}? '
          'The target must be an allowlisted absent or empty directory.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Cancel'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Queue restore'),
          ),
        ],
      ),
    );
    if (confirmed != true) return;
    await _run(
      'Restore queued.',
      (api, token, agentId) => api.startRestore(
        accessToken: token,
        history: history,
        agentId: agentId,
        backupCommandId: _backupCommandId!,
        verificationCommandId: _verificationCommandId!,
        targetPath: _restoreController.text,
      ),
      (id) => _restoreCommandId = id,
    );
  }
}

// Legacy Agent recovery history retained outside the simplified customer dashboard.
// ignore: unused_element
class _HistoryContent extends StatelessWidget {
  const _HistoryContent({required this.runs});
  final List<RecoveryRun> runs;

  @override
  Widget build(BuildContext context) {
    final latest = runs.first;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        _ReadinessCard(run: latest),
        const SizedBox(height: 24),
        Text('Recovery loop', style: Theme.of(context).textTheme.titleLarge),
        const SizedBox(height: 12),
        LayoutBuilder(
          builder: (context, constraints) {
            final columns = constraints.maxWidth >= 900
                ? 4
                : constraints.maxWidth >= 520
                ? 2
                : 1;
            final width = (constraints.maxWidth - (columns - 1) * 12) / columns;
            return Wrap(
              spacing: 12,
              runSpacing: 12,
              children: [
                for (final stage in latest.stages)
                  SizedBox(
                    width: width,
                    child: _StageCard(stage: stage),
                  ),
              ],
            );
          },
        ),
        const SizedBox(height: 28),
        Text(
          'Recent recovery runs',
          style: Theme.of(context).textTheme.titleLarge,
        ),
        const SizedBox(height: 12),
        for (final run in runs)
          Card(
            child: ListTile(
              leading: const Icon(Icons.dns_outlined),
              title: Text(run.agentName),
              subtitle: Text(run.startedAt.toLocal().toString()),
              trailing: _StatusBadge(status: run.status),
            ),
          ),
      ],
    );
  }
}

class _CenteredCard extends StatelessWidget {
  const _CenteredCard({
    required this.icon,
    required this.title,
    required this.message,
    this.action,
  });
  final IconData icon;
  final String title;
  final String message;
  final Widget? action;

  @override
  Widget build(BuildContext context) => Center(
    child: ConstrainedBox(
      constraints: const BoxConstraints(maxWidth: 560),
      child: Card(
        child: Padding(
          padding: const EdgeInsets.all(32),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(icon, size: 46),
              const SizedBox(height: 18),
              Text(
                title,
                style: Theme.of(context).textTheme.headlineSmall,
                textAlign: TextAlign.center,
              ),
              const SizedBox(height: 10),
              Text(message, textAlign: TextAlign.center),
              if (action != null) ...[const SizedBox(height: 22), action!],
            ],
          ),
        ),
      ),
    ),
  );
}

class _ReadinessCard extends StatelessWidget {
  const _ReadinessCard({required this.run});
  final RecoveryRun run;

  @override
  Widget build(BuildContext context) {
    final completed = run.status == RecoveryRunStatus.completed;
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Row(
          children: [
            Icon(
              completed ? Icons.shield_rounded : Icons.pending_actions_rounded,
              size: 42,
            ),
            const SizedBox(width: 18),
            Expanded(
              child: Text(
                completed
                    ? 'Recovery loop proven'
                    : 'Recovery loop in progress',
                style: Theme.of(context).textTheme.titleLarge,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _StageCard extends StatelessWidget {
  const _StageCard({required this.stage});
  final RecoveryStage stage;
  static const labels = {
    RecoveryStageKind.scan: (Icons.radar_rounded, 'Scan'),
    RecoveryStageKind.backup: (Icons.inventory_2_rounded, 'Backup'),
    RecoveryStageKind.verify: (Icons.verified_rounded, 'Verify'),
    RecoveryStageKind.restore: (Icons.restore_rounded, 'Restore'),
  };

  @override
  Widget build(BuildContext context) {
    final label = labels[stage.kind]!;
    final completed = stage.status == RecoveryStageStatus.completed;
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(18),
        child: Row(
          children: [
            Icon(label.$1, size: 30),
            const SizedBox(width: 14),
            Expanded(
              child: Text(
                label.$2,
                style: Theme.of(context).textTheme.titleMedium,
              ),
            ),
            Icon(completed ? Icons.check_circle : Icons.radio_button_unchecked),
          ],
        ),
      ),
    );
  }
}

class _StatusBadge extends StatelessWidget {
  const _StatusBadge({required this.status});
  final RecoveryRunStatus status;

  @override
  Widget build(BuildContext context) => Chip(
    label: Text(switch (status) {
      RecoveryRunStatus.pending => 'Pending',
      RecoveryRunStatus.inProgress => 'In progress',
      RecoveryRunStatus.completed => 'Completed',
      RecoveryRunStatus.failed => 'Failed',
    }),
  );
}
