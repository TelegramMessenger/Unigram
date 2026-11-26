//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Common;
using Telegram.Controls.Messages;
using Telegram.Converters;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Stories;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace Telegram.Controls.Stories
{
    /// <summary>
    /// Either the like button of a story, standing for whichever reaction is chosen on it, or a
    /// suggested reaction area, standing for its own. A click changes nothing here: TDLib applies a
    /// story reaction locally, and the viewer redraws the buttons from the update that follows.
    /// </summary>
    public partial class StoryReactionButton : ReactionButtonBase
    {
        private StoryViewModel _story;
        private ReactionType _type;
        private bool _suggested;
        private bool _chosen;
        private int _count;

        private FrameworkElement _icon;
        private FrameworkElement _glyph;
        private Popup _overlay;

        public StoryReactionButton()
        {
            DefaultStyleKey = typeof(StoryReactionButton);

            Click += OnClick;
        }

        protected override IClientService ClientService => _story?.ClientService;

        protected override int EmojiSize => _suggested ? 70 : ReactionSize > 0 ? ReactionSize * 32 / 20 : 32;

        protected override int CustomEmojiSize => _suggested ? 52 : ReactionSize > 0 ? ReactionSize : 20;

        /// <summary>
        /// The size of a custom emoji reaction, an emoji one being drawn larger by the same ratio
        /// as elsewhere, in a box of this size that the button keeps whatever the reaction is.
        /// Left unset, the reaction is drawn as on a message, and the button is as large as it.
        /// </summary>
        public int ReactionSize { get; set; }

        /// <summary>
        /// Whether the like button shows an empty heart while no reaction is chosen, as the reply
        /// bars of the other apps do, rather than the heart a click would react with.
        /// </summary>
        public bool ShowsEmptyHeart { get; set; }

        // Nothing scrolls these out of view in the stories viewer.
        protected override bool IsCustomEmojiViewportAware => false;

        public override string GetAutomationName()
        {
            return _type switch
            {
                ReactionTypeEmoji emoji => Locale.Declension(Strings.R.AccDescrNumberOfPeopleReactions, _count, emoji.Emoji),
                ReactionTypeCustomEmoji => Locale.Declension(Strings.R.AccDescrNumberOfPeopleReactions, _count, Strings.AccDescrCustomEmoji2),
                _ => null
            };
        }

        public void SetReaction(StoryViewModel story)
        {
            Update(story, story.ChosenReactionType ?? new ReactionTypeEmoji("❤"), false);
        }

        public void SetSuggestedReaction(StoryViewModel story, ReactionType reactionType)
        {
            Update(story, reactionType, true);
        }

        private void Update(StoryViewModel story, ReactionType reactionType, bool suggested)
        {
            if (!IsTemplateApplied)
            {
                _story = story;
                _type = reactionType;
                _suggested = suggested;
                return;
            }

            var sameStory = story.Id == _story?.Id
                && story.PosterChatId == _story?.PosterChatId;

            if (!sameStory)
            {
                StopAnimation();
            }

            // The like button stays the same button when the chosen reaction changes type.
            var recycled = sameStory && (!suggested || reactionType.AreTheSame(_type));
            var chosen = story.ChosenReactionType != null && story.ChosenReactionType.AreTheSame(reactionType);

            _story = story;
            _type = reactionType;
            _suggested = suggested;

            IsChecked = chosen;
            UpdateCount(story, reactionType, suggested, recycled && chosen != _chosen);

            // Taken back while its effect still plays, which would go on around the empty heart.
            var empty = ShowsEmptyHeart && !suggested && story.ChosenReactionType == null;
            if (empty && sameStory && _chosen)
            {
                StopAnimation();
            }

            _chosen = chosen;

            UpdateIcon(story.ClientService, reactionType);
            ShowEmptyHeart(empty);
            FitIcon();
        }

        /// <summary>
        /// Shows <paramref name="reactionType"/>, just chosen somewhere else than this button, and
        /// plays its effect. The update that follows finds it shown already, and leaves it be.
        /// </summary>
        public void Choose(ReactionType reactionType)
        {
            var story = _story;
            if (story == null || _suggested || !IsTemplateApplied)
            {
                return;
            }

            // An earlier choice may still be playing, or about to.
            StopAnimation();

            _type = reactionType;
            _chosen = true;

            IsChecked = true;

            UpdateIcon(story.ClientService, reactionType);
            ShowEmptyHeart(false);
            FitIcon();

            Animate();
        }

        /// <summary>
        /// Ends the effect playing, which plays in a popup and so outlives whatever closes the
        /// button.
        /// </summary>
        public void Stop()
        {
            StopAnimation();
        }

        private void ShowEmptyHeart(bool empty)
        {
            if (_glyph == null || _icon == null)
            {
                return;
            }

            _glyph.Visibility = empty
                ? Visibility.Visible
                : Visibility.Collapsed;

            _icon.Visibility = empty
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private void FitIcon()
        {
            if (ReactionSize <= 0 || _icon == null || _overlay == null)
            {
                return;
            }

            // Larger than the box, as an emoji's animation is, the icon would be laid out from the
            // box's corner rather than centered on it: given room for it, it stays centered, and
            // the box, and so the button, keeps its size whatever the reaction is. The effect,
            // three times an emoji's size, is centered on the box too.
            _icon.Margin = new Thickness(Math.Min(0, (ReactionSize - _icon.Width) / 2));
            _overlay.Margin = new Thickness((ReactionSize - EmojiSize * 3) / 2.0);
        }

        private void UpdateCount(StoryViewModel story, ReactionType reactionType, bool suggested, bool animate)
        {
            var count = 0;

            if (suggested)
            {
                if (story.Areas != null)
                {
                    foreach (var area in story.Areas)
                    {
                        if (area.Type is StoryAreaTypeSuggestedReaction suggestedReaction && suggestedReaction.ReactionType.AreTheSame(reactionType))
                        {
                            count = suggestedReaction.TotalCount;
                            break;
                        }
                    }
                }
            }
            else
            {
                count = story.InteractionInfo?.ReactionCount ?? 0;
            }

            _count = count;

            if (count > 0)
            {
                Count.Visibility = Visibility.Visible;
                Count.SetText(Formatter.ShortNumber(count), animate);
            }
            else
            {
                Count.Visibility = Visibility.Collapsed;
            }
        }

        protected override void OnTemplateApplied()
        {
            _icon = GetTemplateChild("Icon") as FrameworkElement;
            _glyph = GetTemplateChild("Glyph") as FrameworkElement;
            _overlay = GetTemplateChild("Overlay") as Popup;

            // Sized as the other buttons of a bar, its highlight takes their corners as well.
            if (ReactionSize > 0 && GetTemplateChild("RootGrid") is Grid root)
            {
                root.CornerRadius = CornerRadius;
            }

            if (_type != null)
            {
                Update(_story, _type, _suggested);
            }
        }

        private void OnClick(object sender, RoutedEventArgs e)
        {
            var story = _story;
            if (story == null || !IsTemplateApplied)
            {
                return;
            }

            if (_suggested)
            {
                // An area sets its reaction and never takes it back. Its effect plays on every click,
                // on one's own story too, which can't be reacted to.
                if (!_chosen && story.PosterChatId != story.ClientService.Options.MyId)
                {
                    story.ClientService.Send(new SetStoryReaction(story.PosterChatId, story.Id, _type, false));
                }

                Animate();
            }
            else if (story.ChosenReactionType != null)
            {
                story.ClientService.Send(new SetStoryReaction(story.PosterChatId, story.Id, null, false));
            }
            else
            {
                ShowEmptyHeart(false);

                Animate();
                story.ClientService.Send(new SetStoryReaction(story.PosterChatId, story.Id, _type, false));
            }
        }
    }
}
